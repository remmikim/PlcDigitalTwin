/*
 * Decima/Services/FirebaseService.cs
 * IFirebaseService 인터페이스의 실제 구현체입니다.
 * Cloud Firestore를 사용하여 데이터 통신을 관리합니다. (전면 수정)
 */
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Decima.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Decima.Services
{
    public class FirebaseService : IFirebaseService
    {
        private readonly ILogger<FirebaseService> _logger;
        private readonly FirebaseConfig _config;
        private FirestoreDb? _firestoreDb;
        private Func<string, Dictionary<string, object>, Task>? _commandHandler;

        public FirebaseService(IOptions<ServerConfig> config, ILogger<FirebaseService> logger)
        {
            _config = config.Value.Firebase;
            _logger = logger;
        }

        public void Initialize(Func<string, Dictionary<string, object>, Task> commandHandler)
        {
            _commandHandler = commandHandler;

            try
            {
                // 1. 서비스 계정 파일에서 Project ID를 안전하게 읽어옵니다.
                string projectId = GetProjectIdFromFile(_config.ServiceAccountKeyPath);

                // 2. FirebaseApp을 초기화합니다.
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(_config.ServiceAccountKeyPath),
                    });
                }

                // 3. 읽어온 Project ID로 FirestoreDb 인스턴스를 생성합니다.
                _firestoreDb = FirestoreDb.Create(projectId);
                _logger.LogInformation("Cloud Firestore initialized successfully for project {ProjectId}.", projectId);

                ListenForCommands();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize Cloud Firestore. Please check the service account key path or its content.");
                throw;
            }
        }

        public async Task WriteDataAsync(Dictionary<string, object> updates)
        {
            if (_firestoreDb == null)
            {
                throw new InvalidOperationException("Firestore service is not initialized.");
            }

            var batch = _firestoreDb.StartBatch();

            foreach (var update in updates)
            {
                var pathParts = update.Key.Split(new[] { '/' }, 2);
                if (pathParts.Length != 2)
                {
                    _logger.LogWarning("Invalid update key format: {Key}. Expected 'collection/documentId'.", update.Key);
                    continue;
                }

                string collection = pathParts[0];
                string document = pathParts[1];

                DocumentReference docRef = _firestoreDb.Collection(collection).Document(document);
                // SetAsync에 MergeAll 옵션을 주어 문서가 있으면 업데이트, 없으면 생성하도록 합니다.
                batch.Set(docRef, update.Value, SetOptions.MergeAll);
            }

            try
            {
                await batch.CommitAsync();
                _logger.LogDebug("Successfully committed a batch of {Count} updates to Firestore.", updates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while committing a batch to Firestore.");
            }
        }

        private void ListenForCommands()
        {
            if (_firestoreDb == null) return;

            CollectionReference commandQueueRef = _firestoreDb.Collection("command_queue");

            commandQueueRef.Listen(snapshot =>
            {
                foreach (DocumentChange change in snapshot.Changes)
                {
                    if (change.ChangeType == DocumentChange.Type.Added)
                    {
                        _logger.LogInformation("New command document received from Firestore: {DocumentId}", change.Document.Id);

                        var commandData = change.Document.ToDictionary();

                        if (_commandHandler != null)
                        {
                            // 핸들러를 비동기적으로 호출하지만, Listen 콜백에서는 await하지 않습니다.
                            _ = _commandHandler(change.Document.Id, commandData);
                        }
                    }
                }
            });

            _logger.LogInformation("Now listening for new commands in Firestore collection '/command_queue'.");
        }

        /// <summary>
        /// 서비스 계정 키 JSON 파일에서 프로젝트 ID를 파싱하는 private 헬퍼 메서드입니다.
        /// </summary>
        /// <param name="path">서비스 계정 키 파일의 경로</param>
        /// <returns>추출된 프로젝트 ID</returns>
        private string GetProjectIdFromFile(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("project_id", out var projectIdElement))
                {
                    return projectIdElement.GetString() ?? throw new InvalidOperationException("'project_id' property is null in the service account file.");
                }
                throw new InvalidOperationException("Could not find 'project_id' in the service account file.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading or parsing the service account key file at {Path}", path);
                throw;
            }
        }
    }
}

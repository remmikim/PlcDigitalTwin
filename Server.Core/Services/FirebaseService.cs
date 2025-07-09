/*
 * Server.Core/Services/FirebaseService.cs
 * IFirebaseService 인터페이스의 실제 구현체입니다.
 * FirebaseAdmin SDK를 사용하여 Realtime Database와의 통신을 관리합니다.
 */
using FirebaseAdmin;
using FirebaseAdmin.Database; // Firebase Realtime Database를 사용하기 위해 필요한 네임스페이스
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Server.Core.Services
{
    public class FirebaseService : IFirebaseService
    {
        private readonly ILogger<FirebaseService> _logger;
        private readonly FirebaseConfig _config;
        private FirebaseDatabase? _database;
        private Func<string, object, Task>? _commandHandler;

        public FirebaseService(IOptions<ServerConfig> config, ILogger<FirebaseService> logger)
        {
            _config = config.Value.Firebase;
            _logger = logger;
        }

        public void Initialize(Func<string, object, Task> commandHandler)
        {
            _commandHandler = commandHandler;

            try
            {
                // FirebaseApp이 이미 초기화되었는지 확인
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions()
                    {
                        Credential = GoogleCredential.FromFile(_config.ServiceAccountKeyPath),
                        DatabaseUrl = new Uri("https://plcdigitaltwin-default-rtdb.asia-southeast1.firebasedatabase.app/")
                    });
                }

                _database = FirebaseDatabase.DefaultInstance;
                _logger.LogInformation("Firebase Admin SDK initialized successfully.");

                ListenForCommands();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize Firebase Admin SDK. Please check the service account key path and database URL.");
                throw;
            }
        }

        public async Task WriteDataAsync(Dictionary<string, object> updates)
        {
            if (_database == null)
            {
                throw new InvalidOperationException("Firebase service is not initialized.");
            }

            try
            {
                var dbRef = _database.RootReference;
                await dbRef.UpdateAsync(updates);
                _logger.LogDebug("Successfully wrote {Count} updates to Firebase.", updates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while writing data to Firebase.");
            }
        }

        private void ListenForCommands()
        {
            if (_database == null) return;

            var commandQueueRef = _database.GetReference("command_queue");

            commandQueueRef.ChildAdded += async (sender, args) =>
            {
                if (args.Snapshot.Exists)
                {
                    var commandId = args.Snapshot.Key;
                    var commandData = args.Snapshot.Value;

                    _logger.LogInformation("New command received from Firebase: {CommandId}", commandId);

                    if (_commandHandler != null && commandData != null)
                    {
                        await _commandHandler(commandId, commandData);
                    }
                }
            };

            _logger.LogInformation("Now listening for new commands in Firebase at '/command_queue'.");
        }
    }
}
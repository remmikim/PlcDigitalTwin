Hermes PLC Transmitter 개발 설명서
문서 버전: 1.0
작성일: 2025년 7월 9일

1. 개요
본 문서는 "Hermes PLC Transmitter" WPF 애플리케이션의 개발 과정과 아키텍처 설계를 기술한 설명서입니다. 이 애플리케이션의 주요 목적은 여러 PLC 시뮬레이터와 안정적으로 통신하고, 수집된 데이터를 중앙 서버(MQTT 브로커)로 전송하며, 중앙 서버로부터의 제어 명령을 수신하여 해당 PLC에 전달하는 것입니다.

이 문서는 프로젝트의 유지보수 및 인수인계를 위해 작성되었으며, 후임 개발자가 프로젝트의 전체적인 흐름과 기술적 결정 사항을 쉽게 이해하는 것을 목표로 합니다.

2. Part 1: 기초 아키텍처 및 단일 PLC 통신 구현
프로젝트의 첫 번째 파트는 애플리케이션의 기본 뼈대를 구축하고, 가장 핵심적인 기술 과제인 단일 PLC와의 안정적인 통신을 구현하는 데 중점을 두었습니다.

단계 1: 프로젝트 생성 및 MVVM 패턴 기반 구축
프로젝트 생성: .NET 8 기반의 C# WPF 애플리케이션으로 Hermes 프로젝트를 생성했습니다.

라이브러리 설치: 중앙 서버와의 통신을 위해 MQTTnet NuGet 패키지를 설치했습니다.

MVVM(Model-View-ViewModel) 패턴 도입: 코드의 유지보수성과 테스트 용이성을 높이기 위해 MVVM 디자인 패턴을 채택했습니다. 이를 위해 아래와 같은 기본 구조를 설정했습니다.

폴더 구조: Views, ViewModels, Models, Services, Helpers 폴더를 생성하여 역할을 분리했습니다.

헬퍼 클래스: 모든 ViewModel의 기반이 되는 ObservableObject (INotifyPropertyChanged 구현)와 UI 이벤트를 처리하는 RelayCommand (ICommand 구현)를 Helpers 폴더에 작성하여 코드 중복을 최소화했습니다.

단계 2: PLC 통신 서비스 구현 (COM Interop 문제 해결)
기술적 과제: Mitsubishi ActUtlType64.dll은 STA(Single-Threaded Apartment) 모델에서만 동작하는 COM 컴포넌트이므로, 일반적인 멀티스레드 환경에서 직접 호출 시 예측 불가능한 오류가 발생합니다.

해결 아키텍처:

PlcCommunicationService 클래스를 구현하여 PLC 통신 관련 로직을 캡슐화했습니다.

이 서비스 내부에 PLC 통신만을 전담하는 **전용 STA 스레드(_plcThread)**를 생성했습니다.

외부(ViewModel)에서의 통신 요청은 스레드에 안전한 **작업 큐(BlockingCollection)**를 통해 전달되도록 설계했습니다.

이를 통해 DLL과의 모든 상호작용이 전용 스레드에서 안전하게 직렬화되어 처리되도록 보장함으로써 COM Interop 문제를 근본적으로 해결하고 UI 응답성을 100% 유지했습니다.

단계 3: 단일 PLC 연결 및 실시간 데이터 폴링
초기 ViewModel 구현: MainViewModel은 단일 IPlcCommunicationService 인스턴스를 사용하여 PLC 연결/해제 로직을 구현했습니다.

실시간 데이터 폴링:

PLC 연결 성공 시, Timer 또는 Task.Run을 사용하여 백그라운드에서 주기적으로(PollingLoopAsync) PLC의 디바이스 값을 읽어오는 로직을 구현했습니다.

문제 발생: 초기에 여러 디바이스를 동시에 읽으려 시도했을 때 "Failed to read device block" 오류가 발생했습니다.

해결: 폴링 로직을 하나의 디바이스를 읽고 완료될 때까지 기다린 후, 다음 디바이스를 읽는 순차적 방식으로 수정하여 문제를 해결했습니다.

양방향 통신 구현: UI에 입력된 값을 PLC에 쓰는 WriteDeviceBlockAsync 기능을 구현하고 ViewModel과 연동하여 기본적인 양방향 통신을 완성했습니다.

3. Part 2: 다중 PLC 관리 및 MQTT 통신 기능 확장
프로젝트의 두 번째 파트는 단일 PLC 통신 구조를 여러 개의 PLC를 동시에 관리할 수 있도록 확장하고, MQTT를 이용한 완전한 양방향 통신 기능을 구현하는 데 중점을 두었습니다.

단계 4: 동적 디바이스 설정 (JSON 도입)
문제점: 모니터링할 PLC 디바이스 주소가 코드에 하드코딩되어 있어 유지보수가 어려웠습니다.

해결:

Newtonsoft.Json 라이브러리를 추가했습니다.

모니터링할 디바이스 목록을 외부 device_map.json 파일에서 관리하도록 변경했습니다.

애플리케이션 시작 시, MainViewModel이 이 JSON 파일을 읽어와 모니터링 대상을 동적으로 설정하도록 로직을 수정했습니다.

단계 5: 다중 PLC 관리 아키텍처 도입
요구사항: 여러 스테이션 번호를 가진 PLC들을 동시에 관리해야 했습니다.

해결 아키텍처:

PlcManagerService 클래스를 새로 구현했습니다. 이 서비스는 내부에 ConcurrentDictionary<int, IPlcCommunicationService>를 두어, 스테이션 번호를 키(Key)로 각 PLC 통신 서비스 인스턴스를 관리합니다.

device_map.json 파일의 구조를 스테이션 번호별로 디바이스 목록을 정의할 수 있도록 변경했습니다.

PlcConnection 모델을 도입하여 각 PLC 연결의 상태(연결 여부, 데이터 목록 등)를 개별적으로 관리하도록 했습니다.

MainViewModel은 이제 PlcManagerService를 통해 특정 스테이션 번호를 지정하여 통신하도록 수정되었습니다.

단계 6: UI/UX 개편 및 MQTT 기능 통합
UI 개편: 다중 PLC를 효율적으로 관리하기 위해 UI를 대대적으로 개편했습니다.

왼쪽 패널: 관리 대상인 모든 PLC 스테이션의 목록과 연결 상태를 표시합니다.

오른쪽 패널: 왼쪽에서 선택된 PLC의 상세 정보(실시간 데이터, 제어 UI)를 표시합니다.

MQTT 서비스 구현:

IMqttClientService 와 MqttClientService를 구현하여 MQTT 통신 로직을 캡슐화했습니다.

발행(Publish): 폴링 루프에서 PLC 데이터를 읽을 때마다, 해당 데이터를 JSON 형식으로 변환하여 MQTT 브로커로 발행하는 기능을 추가했습니다. 토픽은 dt/{site}/{area}/{line}/{plc_id}/{measurement} 구조를 따릅니다.

LWT(Last Will and Testament): 연결이 비정상적으로 끊어질 경우를 대비해, 오프라인 상태 메시지를 자동으로 발행하는 LWT 기능을 구현했습니다.

MQTT 구독 및 명령 처리 (양방향 통신 완성):

구독(Subscribe): MQTT 브로커 연결 시, 중앙 서버로부터의 명령 토픽(cmd/.../plc-+/write)을 와일드카드로 구독하도록 구현했습니다.

명령 처리: OnMqttMessageReceived 핸들러를 구현하여, 명령 토픽으로 메시지가 수신되면 토픽과 페이로드를 파싱하여 목표 스테이션과 디바이스를 식별하고, PlcManagerService를 통해 해당 PLC에 값을 쓰는 로직을 완성했습니다.

4. 결론 및 향후 전망
현재 Hermes PLC Transmitter는 프로젝트 초기 요구사항이었던 다중 PLC와의 안정적인 양방향 통신 및 MQTT를 통한 중앙 서버 연동 기능을 모두 갖춘 상태입니다. 견고한 서비스 지향 아키텍처와 외부 설정 파일 기반의 유연성을 확보하여, 향후 유지보수 및 기능 확장에 용이한 구조를 가지고 있습니다.

다음 단계는 본 Transmitter와 통신할 Part 2: 중앙 서버 애플리케이션 개발이 될 것입니다.

# Unity Native Toolkit (Unity 6)

- Unity 6+에서 네이티브 기능을 제공하는 툴킷입니다.
- 패키지에는 Android/iOS/Windows/macOS용 네이티브 플러그인과 샘플 씬이 포함되며, 다이얼로그·알림·공유·클립보드 등의 네이티브 기능을 싱글톤 API로 사용할 수 있습니다.
- Editor 창을 통해 네이티브 라이브러리와 Gradle/Xcode 설정을 추가하여 빌드 후 프로젝트 정리를 워크플로로 제공합니다.

다른 언어 README:

- English: [README.md](README.md)
- Japanese: [README.ja.md](README.ja.md)

## 버전

- 1.8.0

## 지원 OS 버전

- Android 12 이상
- iOS 18 이상
- Windows 11 이상
- macOS 15 이상

## 기능

### Android

- 다이얼로그 기능
  - 기본 다이얼로그
  - 확인 다이얼로그
  - 단일 선택 다이얼로그
  - 다중 선택 다이얼로그
  - 입력 다이얼로그
  - 로그인 다이얼로그
- 알림 기능
  - 일반 알림 (표시 / 업데이트 / 취소)
  - 예약 알림 (등록 / 상태 확인 / 취소)
  - 액션 알림
  - 전체 화면 알림
  - DecoratedCustomView 알림
  - 진행 알림 (Foreground Service)
- 공유 기능
  - 텍스트 / URL 공유
  - 이미지 / 다중 이미지 공유
  - 파일 / 다중 파일 공유
  - 커스텀 Chooser 액션 (API 34+)
  - 리치 프리뷰
  - 다이렉트 공유 타겟 등록
  - 콜백 포함 공유
  - 대기 중 콜백 취소
- 클립보드 기능
  - 일반 텍스트 / HTML 텍스트 / URI / 다중 텍스트 복사
  - 민감한 콘텐츠 미리보기 억제 (Android 13+)
  - 클립보드 읽기 / 클립 존재 여부 확인 / 본문에 접근하지 않는 메타데이터 조회
  - 클립보드 변경 관찰
  - 게임 활용 사례: 초대 코드, 코드 붙여넣기, 스크린샷 복사

### iOS

- 다이얼로그 기능
  - 기본 다이얼로그
  - 확인 다이얼로그
  - 파괴적 다이얼로그
  - 액션 시트
  - 입력 다이얼로그
  - 로그인 다이얼로그
- 알림 기능
  - 알림 권한 요청 / 권한 상태 확인 / 알림 설정 화면 이동
  - 즉시 알림 (첨부 파일 알림 샘플 포함)
  - 예약 알림 (시간 간격 / 캘린더 / 위치 기반)
  - 알림 업데이트 / 취소 / 전달 완료 알림 삭제 / 상태 조회
  - 배지 개수 설정
  - 카테고리 등록 / 액션 / 텍스트 입력 액션
- 공유 기능
  - 텍스트 / URL / 프리뷰 포함 URL 공유
  - 이미지 / 다중 이미지 공유
  - 파일 / 다중 파일 공유
  - 텍스트와 URL 동시 공유
  - 제목 포함 공유
  - 공유 시트에서 특정 액티비티 제외

### Windows

- 다이얼로그 기능
  - 기본 다이얼로그
  - 파일 선택 다이얼로그
  - 다중 파일 선택 다이얼로그
  - 폴더 선택 다이얼로그
  - 다중 폴더 선택 다이얼로그
  - 파일 저장 다이얼로그
- 알림 기능
  - 즉시 알림
  - 예약 알림
  - 예약 알림 취소
  - 진행률 바 알림 표시 및 업데이트
  - 태그 지정 또는 전체 알림 삭제
  - 알림 권한 설정 쿼리
  - 시스템 알림 설정 열기
  - 알림 활성화 이벤트 수신 (콜드 스타트 포함)

### macOS

- 다이얼로그 기능
  - 기본 다이얼로그
  - 파일 선택 다이얼로그
  - 다중 파일 선택 다이얼로그
  - 폴더 선택 다이얼로그
  - 다중 폴더 선택 다이얼로그
  - 파일 저장 다이얼로그

- 알림 기능
  - 알림 권한 요청 / 권한 상태 확인 / 시스템 알림 설정 열기
  - 즉시 알림
  - 예약 알림 (시간 간격 / 캘린더)
  - 알림 업데이트 / 취소 / 전달된 알림 삭제
  - 예약된 알림 및 전달된 알림 목록 조회
  - 배지 카운트 관리
  - 카테고리 등록 / 액션 / 텍스트 입력 액션

- 공유 기능
  - 텍스트 / URL 공유
  - 이미지 / 다중 이미지 공유
  - 파일 / 다중 파일 공유
  - 텍스트와 URL 동시 공유
  - 서비스 제외 공유
  - 지정 서비스 경유 공유 (Mail 등)

## 설치

- Unity 6을 실행합니다.
- Window -> Package Manager를 선택합니다.
- "install from Git URL..."을 선택합니다.
- Native Toolkit 패키지의 Git URL을 입력합니다.
  - https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.8.0
- "install"을 클릭합니다.
- 요구 사항:
  - Unity 6 이상
  - 의존 패키지: Localization, Addressables, Input System

## 샘플

- Unity 6을 실행합니다.
- Window -> Package Manager를 선택합니다.
- Unity Package Manager -> Native Toolkit -> Samples -> Import를 선택합니다.
- Tools -> Native Toolkit -> Example을 선택합니다.

## 상세 문서

- 패키지 문서를 참고하세요:
  - [영어 문서](docs/latest/index.md)
  - [한국어 문서](docs/latest/index.ko.md)
  - [일본어 문서](docs/latest/index.ja.md)

## Native Toolkit

- Native Toolkit은 플랫폼 네이티브 기능을 통합적으로 제공하는 툴킷입니다.
- Android / iOS / Windows / macOS용 네이티브 플러그인과 샘플이 포함되어 있으며, 다이얼로그·알림·공유 등의 네이티브 기능을 싱글톤 API로 제공합니다.
- Repository: https://github.com/kimjh4941/native-toolkit

## 라이선스

Apache License 2.0 (자세한 내용은 `LICENSE` 참조).

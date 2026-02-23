# Unity Native Toolkit (Unity 6)

- Unity 6+에서 네이티브 기능을 제공하는 툴킷입니다.
- 패키지에는 Android/iOS/Windows/macOS용 네이티브 플러그인과 샘플 씬이 포함되며, 각 플랫폼의 다이얼로그를 싱글톤 API로 사용할 수 있습니다.
- Editor 창을 통해 네이티브 라이브러리와 Gradle/Xcode 설정을 추가하여 빌드 후 프로젝트 정리를 워크플로로 제공합니다.

## 버전

- 1.0.0

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

### iOS

- 다이얼로그 기능
  - 기본 다이얼로그
  - 확인 다이얼로그
  - 파괴적 다이얼로그
  - 액션 시트
  - 입력 다이얼로그
  - 로그인 다이얼로그

### Windows

- 다이얼로그 기능
  - 기본 다이얼로그
  - 파일 선택 다이얼로그
  - 다중 파일 선택 다이얼로그
  - 폴더 선택 다이얼로그
  - 다중 폴더 선택 다이얼로그
  - 파일 저장 다이얼로그

### macOS

- 다이얼로그 기능
  - 기본 다이얼로그
  - 파일 선택 다이얼로그
  - 다중 파일 선택 다이얼로그
  - 폴더 선택 다이얼로그
  - 다중 폴더 선택 다이얼로그
  - 파일 저장 다이얼로그

## 추가 예정 기능

- 공유
- 클립보드 연동
- 알림

## 설치

- Unity 6을 실행합니다.
- Window -> Package Manager를 선택합니다.
- "install from Git URL..."을 선택합니다.
- Native Toolkit 패키지의 Git URL을 입력합니다.
  - https://github.com/jonghyunkim/unity-native-plugin.git?path=/Packages/com.jonghyunkim.nativetoolkit#1.0.0
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
- Android / iOS / Windows / macOS용 네이티브 플러그인과 샘플이 포함되어 있으며, 다이얼로그 API를 싱글톤 인터페이스로 제공합니다.
- Repository: https://github.com/kimjh4941/native-toolkit

## 라이선스

Apache License 2.0 (자세한 내용은 `LICENSE` 참조).

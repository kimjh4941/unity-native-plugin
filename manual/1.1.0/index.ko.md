# Unity Native Toolkit (Unity 6)

[English](index.md) | [Korean](index.ko.md) | [Japanese](index.ja.md)

- Unity 6+에서 네이티브 기능을 제공하는 툴킷입니다.
- 패키지에는 Android/iOS/Windows/macOS용 네이티브 플러그인과 샘플 씬이 포함되며, 각 플랫폼의 다이얼로그를 싱글톤 API로 사용할 수 있습니다.
- Editor 창을 통해 네이티브 라이브러리와 Gradle/Xcode 설정을 추가하여 빌드 후 프로젝트 정리를 워크플로로 제공합니다.

# 버전

## 1.0.0

# 지원 OS 버전

- Android 12 이상
- iOS 18 이상
- Windows 11 이상
- macOS 15 이상

# 기능

## Android

- 다이얼로그 기능
  - 기본 다이얼로그
  - 확인 다이얼로그
  - 단일 선택 다이얼로그
  - 다중 선택 다이얼로그
  - 입력 다이얼로그
  - 로그인 다이얼로그

## iOS

- 다이얼로그 기능
  - 기본 다이얼로그
  - 확인 다이얼로그
  - 파괴적 다이얼로그
  - 액션 시트
  - 입력 다이얼로그
  - 로그인 다이얼로그

## Windows

- 다이얼로그 기능
  - 기본 다이얼로그
  - 파일 선택 다이얼로그
  - 다중 파일 선택 다이얼로그
  - 폴더 선택 다이얼로그
  - 다중 폴더 선택 다이얼로그
  - 파일 저장 다이얼로그

## macOS

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

# 시작하기

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
<p align="center">
  <img src="images/editor/NativeToolkitEditorWindow.png" alt="NativeToolkit Editor Window" width="720" />
</p>

- Android 샘플
  - Android - Dialog - AndroidDialogManager.cs를 선택합니다.
  - "Open" 버튼을 클릭합니다.
  - Game 뷰에 샘플 화면이 표시됩니다.
  - Build Profiles에서 "Android Profile" -> Export를 실행합니다.
  - Tools -> Native Toolkit -> Android -> Configure Gradle Project를 선택합니다.
  <p align="center">
    <img src="images/editor/ConfigureGradleProject.png" alt="ConfigureGradleProject" width="720" />
  </p>

  - "Browse"를 클릭하고 Export한 Android 프로젝트를 선택합니다.
  - "Run: Add Kotlin Dependencies"를 클릭하여 Kotlin 라이브러리를 추가합니다.
  - Android Studio에서 샘플 앱을 설치합니다.
    - <a href="https://developer.android.com/studio" target="_blank" rel="noopener noreferrer">참고</a>

- iOS 샘플
  - iOS - Dialog - IosDialogManager.cs를 선택합니다.
  - "Open" 버튼을 클릭합니다.
  - Game 뷰에 샘플 화면이 표시됩니다.
  - Build Profiles에서 "iOS Profile" -> Build를 실행합니다.
  - Tools -> Native Toolkit -> iOS -> Configure Xcode Project를 선택합니다.
  <p align="center">
    <img src="images/editor/IosConfigureXcodeProject.png" alt="IosConfigureXcodeProject" width="720" />
  </p>

  - "Browse"를 클릭하고 빌드된 iOS 프로젝트를 선택합니다.
  - "Run: Add/Embed iOS XCFrameworks"를 클릭하여 NativeToolkit 라이브러리를 추가합니다.
  - Xcode에서 샘플 앱을 설치합니다.
    - <a href="https://developer.apple.com/xcode" target="_blank" rel="noopener noreferrer">참고</a>

- Windows 샘플
  - Windows - Dialog - WindowsDialogManager.cs를 선택합니다.
  - "Open" 버튼을 클릭합니다.
  - Game 뷰에 샘플 화면이 표시됩니다.
  - Build Profiles에서 "Windows Profile" -> Build를 실행합니다.
  - 빌드 출력 폴더의 "Unity NativeToolkit.exe"를 실행합니다.

- macOS 샘플
  - macOS - Dialog - MacDialogManager.cs를 선택합니다.
  - "Open" 버튼을 클릭합니다.
  - Game 뷰에 샘플 화면이 표시됩니다.
  - Build Profiles에서 "macOS Profile" -> Build를 실행합니다.
  - Tools -> Native Toolkit -> macOS -> Configure Xcode Project를 선택합니다.
  <p align="center">
    <img src="images/editor/MacConfigureXcodeProject.png" alt="MacConfigureXcodeProject" width="720" />
  </p>

  - "Browse"를 클릭하고 빌드된 macOS 프로젝트를 선택합니다.
  - "Run: Add UnityMacNativeToolkit.xcframework"를 클릭하여 NativeToolkit 라이브러리를 추가합니다.
  - Xcode에서 샘플 앱을 설치합니다.
    - <a href="https://developer.apple.com/xcode" target="_blank" rel="noopener noreferrer">참고</a>


# API 사용법

- [다이얼로그](dialog.ko.md)
- [알림](notification.ko.md)

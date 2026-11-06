# 왕샷 (WangShot)

교재·문서 집필용 스크린샷 프로그램. 오픈소스 [Greenshot](https://github.com/greenshot/greenshot)(GPL-3.0)을 기반으로 만들었어요.

## 주요 기능

- **PrintScreen** 으로 창·대화상자·사각형 영역 캡처 (창 위에서 클릭 = 창, 드래그 = 영역)
- 캡처 후 **에디터** 자동 열기 + 지정 폴더에 **번호 순서대로 자동 저장** (`001.png`, `002.png` …)
- 파일 이름 접두어(장·절) 지정: 설정 → 출력 → `파일 접두어`, 바꾸면 번호가 001부터 다시 시작
- **스크롤 캡처**: 트레이 메뉴 → `스크롤 캡처 (마우스 아래 창)` — 마우스가 있는 창(브라우저 등)을 자동으로 스크롤하며 이어 붙여요 (고정 머리글·바닥글 처리, Esc로 중단)
- 리본 메뉴 에디터, 색 팔레트, 눈금자, 다른 이름으로 저장에 **PDF** 지원
- 에디터: 선, 사각형, 타원, 화살표, 연필, 텍스트, 말풍선, 형광펜, 이모지(빠른 선택), **번호(원·사각형·삼각형)**
- 에디터: 드래그 크롭(놓으면 바로 적용), 이미지 밖까지 크롭/그리기 하면 **캔버스 자동 확장**
- Ctrl+C = 이미지 전체 복사(그림판 등에 붙여넣기), Ctrl+Shift+C = 선택한 객체 복사
- 마우스 커서 캡처 ON/OFF (트레이 메뉴)
- Windows 의 "PrintScreen 으로 캡처 도구 열기" 설정과 충돌하면 시작할 때 알려주고 끌 수 있게 도와줘요
- 트레이/작업표시줄에서 **편집기 열기**

## 빌드

.NET 10 SDK 와 .NET Framework 4.8 개발자 팩이 필요해요.

```
dotnet build src/Greenshot/Greenshot.csproj -c Release
```

결과: `src/Greenshot/bin/Release/net480/WangShot.exe`

## 라이선스

GPL-3.0-or-later. 원본 Greenshot 의 저작권은 Greenshot 개발자들에게 있고, 원본 설명서는 [README.greenshot.md](README.greenshot.md) 에 남겨 두었어요.

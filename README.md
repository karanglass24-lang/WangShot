# 왕샷 (WangShot)

교재·문서 집필용 스크린샷 프로그램. 오픈소스 [Greenshot](https://github.com/greenshot/greenshot)(GPL-3.0)을 기반으로 만들었어요.

만든 곳: [두목넷](https://dumok.net)

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

Windows + **Visual Studio 2026**(Desktop development with C++ 워크로드 — MSVC v145 x64/x86 빌드 도구, Windows 10 SDK 포함) 또는 그에 준하는 **MSBuild/Build Tools** + **.NET Framework 4.8 개발자 팩**이 필요해요. 솔루션 생성 태스크(`Greenshot.BuildTasks`)와 `greenshot-proxy`(C++) 때문에 **Visual Studio의 MSBuild**로 빌드하는 게 정석이고, `dotnet build`/`dotnet msbuild`는 이 솔루션에서 그대로 믿기 어렵다는 게 저장소 자체 문서의 정리예요(`.github/copilot-instructions.md`의 빌드 요건·'Issue 1' 참고). 그리고 `src/global.json` 기준으로 **.NET 10 SDK**도 필요해요(`rollForward: latestMajor`, Greenshot.Mcp가 net10.0-windows 대상).

전체 솔루션 — CI·릴리스와 동일:

```powershell
msbuild src/Greenshot.sln /p:Configuration=Release /t:Rebuild /v:normal
```

- 디버깅: `/p:Configuration=Debug` · Light 에디션: `DebugLight`/`ReleaseLight` 구성 또는 `/p:GreenshotEdition=Light`
- 단일 프로젝트만 빠르게 확인할 때 `dotnet build`를 쓰는 경우도 있지만(저장소의 `README.greenshot.md`가 Light 경로를 예시로 듦), 릴리스 검증 기준선은 위 MSBuild 경로예요.

결과물: `src/Greenshot/bin/Release/net480/WangShot.exe` (플러그인 `Plugins/`, 언어팩 `Languages/`)

## Known vulnerability warnings (알려진 취약점 경고)

릴리스 빌드는 NuGet 감사(NuGetAudit) 경고 5건을 **의도적으로 억제**하고 있어요 — 전부 `SixLabors.ImageSharp`(현재 2.1.13 고정) 관련이고, 사유·재검토 조건은 `src/Directory.Build.props`의 `NuGetAuditSuppress` 주석이 원본이에요. 2026-10-07 공개된 공지들이고, 작성 시점 기준 **알려진 실제 악용 정황은 없음**이에요.

- **GHSA-gwg2-r3hj-4w44** (CVE-2026-106114, CVSS 5.3) — ICC 프로필 CLUT 파싱이 값 검증 전에 대용량 할당. [공지](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-gwg2-r3hj-4w44)
- **GHSA-j3p4-wp97-rph4** (CVE-2026-106113, CVSS 7.5) — `HistogramEqualization` 이 검증 안 된 휘도 값을 히스토그램 인덱스로 사용. [공지](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-j3p4-wp97-rph4)
- **GHSA-j9gm-c75j-xc9q** (CVE-2026-106110, CVSS 7.5) — TIFF CCITT T4 인코더가 압축 버퍼를 초과해 기록. [공지](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-j9gm-c75j-xc9q)
- **GHSA-jjfr-hcj7-qf5w** (CVE-2026-106115, CVSS 7.5) — TIFF CCITT T6 인코더가 압축 버퍼를 초과해 기록. [공지](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-jjfr-hcj7-qf5w)
- **GHSA-wmxv-xphr-5c9g** (CVE-2026-106116, CVSS 5.3) — BigTIFF IFD 엔트리 수 미검증으로 입력이 안 줄어도 도는 루프. [공지](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-wmxv-xphr-5c9g)

패치 버전은 4.1.2 / 3.2.0 라인인데 net480 지원 여부가 관건이라, 그걸 만족하는 버전이 나오면 올리고 이 억제들을 지우면 돼요.

## 라이선스

GPL-3.0-or-later. 원본 Greenshot 의 저작권은 Greenshot 개발자들에게 있고, 원본 설명서는 [README.greenshot.md](README.greenshot.md) 에 남겨 두었어요.

## 만든 사람

왕두목 · [두목넷 dumok.net](https://dumok.net) — 정보처리·사무자동화·컴활 자격증 온라인 강의

- 학습 블로그: https://dumok.net/blog
- 유튜브: https://www.youtube.com/@extudy

# 왕샷(WangShot) 인수인계 문서

**작성일**: 2026-11-18  
**현재 버전**: 1.5.1  
**담당자**: karanglass24-lang (GitHub)  
**저장소**: https://github.com/karanglass24-lang/WangShot

---

## 1. 프로젝트 개요

왕샷은 교재·문서 집필용 스크린샷 프로그램입니다. 오픈소스 [Greenshot](https://github.com/greenshot/greenshot)(GPL-3.0)을 기반으로 하며, 한국어 UI와 교재 작성 특화 기능을 제공합니다.

**주요 목적**:
- 교재·문서 작성 시 빠른 스크린샷 캡처 및 편집
- 번호 순서대로 자동 저장 (`001.png`, `002.png` …)
- 리본 메뉴 에디터로 직관적인 객체 편집

---

## 2. 현재 릴리스 현황 (v1.5.1)

### 📦 배포된 자산
| 자산 | 크기 | 다운로드 |
|---|---|---|
| `WangShot-1.5.1-setup.exe` | 13.3 MB | [GitHub Releases](https://github.com/karanglass24-lang/WangShot/releases/tag/v1.5.1) |
| `WangShot-v1.5.1-win-x64.zip` | 11.8 MB | 포터블 버전 |

### 🏷️ 릴리스 태그 현황
- **v1.5.1** (Latest) — 커밋 `0697d80c` — 2026-11-18 (편집기 미실행 버그 수정)
- **v1.5.0** — 커밋 `8cefabf0` — 2026-11-18 (Smart Move / 컨텍스트 탭 / KeyTips, **편집기 미실행 버그 있음**)
- **v1.4.0** — 커밋 `8e364886` — 2026-11-18
- **v1.3.0** — 커밋 `1deaa389` — 2026-11-18
- 수동 릴리스: v1.0.0 ~ v1.2.1
- GitHub Actions 연속 빌드: v1.3.55 ~ v1.4.450 (자동 생성, 최신 5개만 유지)

> ⚠️ **1.5.0은 사용하지 마세요.** 에디터가 전혀 열리지 않는 치명적 버그가 있었고, 1.5.1에서 수정되었습니다.

---

## 3. 1.5.0 신규 기능 상세

### 3.1 Smart Move (내용 인식 이동)
- **트리거**: `Ctrl` + `Shift` + 드래그
- **동작**: 개체 이동 시 원래 위치의 배경을 주변 텍스처로 자동 인페인팅
- **구현 파일**: `src/Greenshot.Editor/Drawing/Surface.cs`
  - `StartSmartMove()`: 선택 영역 주변 패딩 포함 배경 캡처
  - `InpaintBackground()`: 반복적 경계 채우기 알고리즘 (3회 이터레이션)
  - `RenderSmartMoveBackground()`: 드래그 중 원본 위치에 인페인팅된 배경 실시간 렌더링
  - `EndSmartMove()`: 리소스 정리
- **한계**: 현재는 단순 패치 기반 인페인팅 (복잡한 텍스처는 부자연스러울 수 있음). 향후 텍스처 합성/딥러닝 기반으로 개선 가능.

### 3.2 컨텍스트 탭 (서식)
- **동작**: 텍스트 편집 시 '서식' 탭 자동 표시
- **구성 그룹**: 글꼴 / 정렬 / 스타일 / 테두리·채우기
- **구현 파일**: 
  - `src/Greenshot.Editor/Drawing/TextContainer.cs`: `TextEditingStarted`/`TextEditingEnded` 이벤트 추가
  - `src/Greenshot.Editor/Forms/ImageEditorForm.Ribbon.cs`: `UpdateFormatContextTab()` 로직
  - `src/Greenshot.Editor/Controls/EditorRibbon.cs`: `SetTabVisible()`/`IsTabVisible()` 메서드

### 3.3 KeyTips (Alt 단축키 오버레이)
- **트리거**: `Alt` 키 단독 누름
- **동작**: Office 스타일 키 팁 배지 표시, 탭/그룹별 한 글자 키로 빠른 접근
- **구현 파일**: `src/Greenshot.Editor/Controls/EditorRibbon.cs`
  - `SetTabKeyTip()`, `SetGroupKeyTip()`: 키 문자 할당
  - `SetKeyTipMode()`: 모드 진입/종료
  - `BuildKeyTipMap()`: 자동 키 문자 할당 (첫 글자 우선, 중복 시 다음 글자/숫자)
  - `OnKeyDown()`: 키 입력 매칭 → 탭 선택/그룹 액션 실행
  - `DrawKeyTips()`: 배지 스타일 오버레이 렌더링

### 3.4 버전 정보 업데이트
- `src/Greenshot/Helpers/WangShotInfo.cs`: `Version = "1.5.1"`
- `installer/wangshot.iss`: `AppVersion "1.5.1"`
- `README.md`: 신규 기능 설명 추가

### 3.5 1.5.1 버그 수정 — 편집기가 열리지 않던 문제

**증상**
- 트레이 아이콘 → `편집기 열기` 를 눌러도 아무 일도 일어나지 않음
- 스크린샷 후 자동 편집기가 실행되지 않음 (캡처는 되지만 창이 안 뜸)

**원인**
`CreateFormatTab()`(1.5.0에서 추가한 서식 컨텍스트 탭)이 **폼 생성자 안에서** `_surface.FieldAggregator` 에 접근했습니다.
`_surface` 는 `AddTab()` 에서 설정되는데, 생성자는 `Initialize()`(→`BuildRibbon()`→`CreateFormatTab()`)를 먼저 호출하므로
이 시점에는 `null` → `NullReferenceException` → 에디터 창 생성 실패.
예외가 메시지 루프에서 조용히 삼켜져 아무 반응이 없어 보였습니다.

```
new ImageEditorForm(...)
  ├─ Initialize()   → BuildRibbon() → CreateFormatTab()   // _surface == null ❌
  └─ AddTab(surface)                                      // 여기서 _surface 설정
```

**수정**
1. `ImageEditorForm.Ribbon.cs`
   - `CreateFormatTab()`: UI 생성만 담당. 컨트롤을 필드(`_formatFontFamilyComboBox` 등)로 보관하고 `_surface` 접근 제거.
   - `BindFormatFieldControls()` 신설: 표면이 연결될 때(`BindFieldControls`) 호출되어 바인딩 수행.
2. `ImageEditorForm.cs`
   - `BindFieldControls()` 끝에서 `BindFormatFieldControls()` 호출.
   - `UnbindSurface()` 에서 텍스트 편집 감시 해제.
   - `FontPropertyChanged`: 변경을 발생시킨 콤보박스의 폰트 패밀리를 사용 + null 안전.
3. 컨텍스트 탭 실제 동작 연결
   - `SyncTextEditingWatchers()` / `TextContainerEditingChanged()` / `ClearTextEditingWatchers()` 추가.
   - `TextContainer.TextEditingStarted/Ended` 이벤트를 구독해 텍스트 편집 중에만 ’서식’ 탭 표시.
4. `TextContainer.cs`: `IsEditing` 속성 추가 (텍스트박스가 보일 때 true).

**검증 (실제 실행)**
```powershell
# 수정 빌드
dotnet build src\Greenshot\Greenshot.csproj -c Release

# 편집기 열기 (트레이 '편집기 열기' 와 동일 경로)
src\Greenshot\bin\Release\net480\WangShot.exe --editor
# → 창 제목: "새 이미지 - 왕샷 이미지 편집기" 확인

# 캡처와 같은 편집기 경로 (EditorDestination)
src\Greenshot\bin\Release\net480\WangShot.exe --file "C:\...\capture-test.png"
# → 창 제목이 파일 경로로 표시되며 정상 열림
```

> 설치된 1.5.0 트레이 앱이 실행 중이면 단일 인스턴스(Mutex) 때문에 새 빌드가 기존 인스턴스로 명령을 전달합니다.
> 테스트 전에 `Get-Process WangShot | Stop-Process -Force` 로 종료하세요.

---

## 4. 주요 파일 구조

```
D:\llm\capture\greenshot\
├── src\
│   ├── Greenshot\                    # 메인 애플리케이션
│   │   └── Helpers\WangShotInfo.cs   # 버전 정보
│   ├── Greenshot.Editor\            # 에디터
│   │   ├── Controls\EditorRibbon.cs # 리본 UI, KeyTips
│   │   ├── Drawing\
│   │   │   ├── Surface.cs           # 캔버스, Smart Move
│   │   │   ├── TextContainer.cs     # 텍스트 객체, 편집 이벤트
│   │   │   └── GroupContainer.cs    # 그룹 객체
│   │   └── Forms\
│   │       ├── ImageEditorForm.cs   # 에디터 폼, 필드 바인딩
│   │       └── ImageEditorForm.Ribbon.cs # 리본 구성
│   └── Greenshot.Tests\             # 테스트
├── installer\wangshot.iss           # Inno Setup 설치 파일
├── README.md                        # 프로젝트 설명
└── .github\workflows\release.yml   # CI/CD 파이프라인
```

---

## 5. 빌드 및 테스트

### 5.1 빌드
```powershell
# 전체 빌드
dotnet build src\Greenshot.sln -c Release

# 에디터만 빌드
dotnet build src\Greenshot.Editor\Greenshot.Editor.csproj -c Release

# 테스트 빌드
dotnet build src\Greenshot.Tests\Greenshot.Tests.csproj -c Release
```

### 5.2 테스트 실행
```powershell
# 전체 테스트
dotnet test src\Greenshot.Tests\Greenshot.Tests.csproj -c Release

# 특정 클래스만
dotnet test src\Greenshot.Tests\Greenshot.Tests.csproj -c Release --filter "FullyQualifiedName~GroupContainerTests"
```

**현재 테스트 현황**:
- `GroupContainerTests`: 8개 (5개 통과, 3개 스킵 - Move/Flip은 테스트 환경 한계)
- 기타: EmphasisToolsTests, SurfaceDragTests, FilterContainerTests, ObfuscationStrengthTests, SurfaceInputStateTests 모두 통과
- `EditorStartupTimingTests`(Trait `Category=Performance`): STA 스레드에서 폼을 Show + `Application.DoEvents` 로 페인트를 기다리는 측정용 테스트.
  원격/비활성 세션·일부 데스크톱 환경에서 **2분 타임아웃으로 실패**할 수 있음(코드 문제 아님). CI에서는 실행되지 않도록 필터 권장.

### 5.3 이 환경에서 확인된 테스트 러너 이슈 (참고)
`dotnet test --filter "...&..."` 조합이나 병렬 실행 중 파일 잠금(`testhost`/`Greenshot.Tests` 가 DLL 을 점유)으로
빌드가 `MSB3021/MSB3027` 로 실패하거나 러너가 멈출 수 있습니다. 대처:

```powershell
# 남은 테스트 프로세스 정리
Get-Process testhost,Greenshot.Tests -ErrorAction SilentlyContinue | Stop-Process -Force

# 빌드 후 xunit 콘솔 러너로 직접 실행 (가장 안정적)
cd src\Greenshot.Tests\bin\Release\net480
.\Greenshot.Tests.exe -namespace Greenshot.Tests.Editor -notrait "Category=Performance"

# 특정 클래스
.\Greenshot.Tests.exe -class Greenshot.Tests.Editor.GroupContainerTests
```

---

## 6. CI/CD 파이프라인

**워크플로우**: `.github/workflows/release.yml`

**트리거**: `main` 브랜치 푸시 (`.github/**` 경로 제외)

**주요 단계**:
1. 빌드 (Debug/Release)
2. 테스트 실행
3. Inno Setup으로 설치 파일 생성
4. GitHub Release 생성 (태그 = WangShotInfo.Version)
5. 자산 업로드 (setup.exe, portable.zip)

**수동 실행**: GitHub Actions → Build and Deploy → Run workflow

---

## 7. 알려진 이슈 및 제약사항

### 7.1 Smart Move 인페인팅 품질
- 현재 단순 반복 채우기 알고리즘으로 복잡한 텍스처/그라데이션에서 부자연스러울 수 있음
- **개선 방향**: PatchMatch 알고리즘, 텍스처 합성, 또는 OpenCV GrabCut/Inpaint API 연동

### 7.2 GroupContainer Move/Flip 테스트
- 테스트 환경에서 마우스 좌표 변환 이슈로 `Group_Move_MovesAllChildren`, `Group_Flip_FlipsChildren` 테스트 스킵
- **개선 방향**: 테스트용 CoordinateMapper 모킹 또는 통합 테스트로 전환

### 7.3 KeyTip 문자 충돌
- 자동 할당 시 같은 첫 글자가 많은 경우 두 번째 글자 또는 숫자로 대체
- **개선 방향**: 수동 키 매핑 UI 제공 또는 더 똑똑한 할당 알고리즘

### 7.4 GitHub Actions 연속 빌드 태그 정리
- v1.3.x, v1.4.x 계열의 자동 생성 태그가 400개 이상 누적됨
- **개선 방향**: 릴리스 워크플로우에서 태그 생성 조건을 수동 릴리스로 한정

### 7.5 폼 생성자에서 `_surface` 접근 금지 (재발 방지)
- **교훈**: `ImageEditorForm.Initialize()` 는 `AddTab()` 보다 먼저 실행되므로 이 시점 `_surface` 는 `null`.
- 리본/툴바 구성 코드는 `_surface` 에 의존하면 안 됨. 표면 의존 바인딩은 `BindFieldControls()`(표면 연결 시 호출)에서 수행할 것.
- 새 UI를 리본에 추가할 때 체크: `BuildRibbon()` 안에서 `_surface`/`SelectedTab` 사용 여부.
- 회귀 확인: `WangShot.exe --editor` 로 에디터가 뜨는지 (가장 빠른 스모크 테스트).

### 7.6 테스트 러너 환경 이슈
- `EditorStartupTimingTests` 는 세션/데스크톱 상태에 따라 2분 타임아웃으로 실패할 수 있음 (5.2/5.3 참고)
- `dotnet test` 실행 중 파일 잠금으로 빌드 실패 시 테스트 프로세스를 정리하고 xunit 콘솔 러너 사용

---

## 8. 향후 로드맵 (1.6.0 후보)

| 우선순위 | 기능 | 예상 노력 | 효과 |
|---|---|---|---|
| **1** | 가리기 민감정보 자동 감지 (전화번호/이메일/주민번호) | 중간 | ★★★ 보안/교재 작성 |
| **2** | Smart Move 인페인팅 품질 향상 (텍스처 합성) | 높음 | ★★★ Snagit 경쟁력 |
| **3** | 에디터 시작 속도 최적화 | 낮~중간 | ★★ 쾌적함 |
| **4** | 다국어 지원 확대 (영어 UI 옵션) | 낮음 | ★★ 접근성 |
| **5** | 플러그인 시스템 고도화 | 높음 | ★★ 확장성 |

---

## 9. 개발 환경

- **IDE**: Visual Studio 2022 / Rider / VS Code + C# Dev Kit
- **.NET**: .NET Framework 4.8 (레거시 Greenshot 베이스)
- **플랫폼**: Windows 10/11
- **필수 도구**:
  - [Inno Setup 6](https://jrsoftware.org/isinfo.php) (빌드 시 설치 파일 생성용)
  - [GitHub CLI](https://cli.github.com/) (릴리스 관리용)

---

## 10. 연락처 및 지원

- **GitHub Issues**: https://github.com/karanglass24-lang/WangShot/issues
- **릴리스 페이지**: https://github.com/karanglass24-lang/WangShot/releases
- **프로젝트 페이지**: https://github.com/karanglass24-lang/WangShot

---

## 11. 빠른 시작 가이드 (신규 개발자용)

### 로컬 개발 환경 설정
```powershell
# 저장소 클론
git clone https://github.com/karanglass24-lang/WangShot.git
cd WangShot

# 빌드
dotnet build src\Greenshot.sln -c Release

# 실행
.\src\Greenshot\bin\Release\net480\WangShot.exe
```

### 새 기능 추가 시 체크리스트
1. `WangShotInfo.Version` 버전 업
2. `installer/wangshot.iss` 버전 동기화
3. `README.md` 기능 설명 추가
4. 관련 테스트 작성/수정
5. `WangShot.exe --editor` / `--file` 로 에디터 스모크 테스트 (조용한 생성자 예외 방지)
6. 로컬 빌드/테스트 통과 확인
7. 커밋 → 푸시 → GitHub Actions 확인
8. 릴리스 노트 검토

---

**문서 끝**  
마지막 업데이트: 2026-11-18 (v1.5.1)  
작성자: WangShot Development Team
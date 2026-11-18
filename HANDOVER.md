# 왕샷(WangShot) 인수인계 문서

**작성일**: 2026-11-18  
**현재 버전**: 1.5.0  
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

## 2. 현재 릴리스 현황 (v1.5.0)

### 📦 배포된 자산
| 자산 | 크기 | 다운로드 |
|---|---|---|
| `WangShot-1.5.0-setup.exe` | 13.3 MB | [GitHub Releases](https://github.com/karanglass24-lang/WangShot/releases/tag/v1.5.0) |
| `WangShot-v1.5.0-win-x64.zip` | 11.8 MB | 포터블 버전 |

### 🏷️ 릴리스 태그 현황
- **v1.5.0** (Latest) — 커밋 `8cefabf0` — 2026-11-18
- **v1.4.0** — 커밭 `8e364886` — 2026-11-18
- **v1.3.0** — 커밋 `1deaa389` — 2026-11-18
- 수동 릴리스: v1.0.0 ~ v1.2.1
- GitHub Actions 연속 빌드: v1.3.55 ~ v1.4.450 (자동 생성, 최신 5개만 유지)

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
- `src/Greenshot/Helpers/WangShotInfo.cs`: `Version = "1.5.0"`
- `installer/wangshot.iss`: `AppVersion "1.5.0"`
- `README.md`: 신규 기능 설명 추가

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
5. 로컬 빌드/테스트 통과 확인
6. 커밋 → 푸시 → GitHub Actions 확인
7. 릴리스 노트 검토

---

**문서 끝**  
마지막 업데이트: 2026-11-18  
작성자: WangShot Development Team
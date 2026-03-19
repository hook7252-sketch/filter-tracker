# CivilRun_MadeByHouuu AutoCAD Plugin

AutoCAD용 생산성 향상 플러그인입니다. 드림플러스처럼 자주 쓰는 작업을 빠르게 실행할 수 있습니다.

## 빌드 방법

### 요구 사항
- Visual Studio 2022 이상 또는 .NET SDK 4.8
- AutoCAD 2024 (또는 하위 버전 — 버전에 맞게 NuGet 패키지 변경)

### 빌드

```bash
cd CivilRun_MadeByHouuu
dotnet build -c Release
```

또는 Visual Studio에서 `CivilRun_MadeByHouuu.csproj`를 열고 빌드합니다.

> **참고**: AutoCAD가 설치된 환경에서는 `CivilRun_MadeByHouuu.csproj`의 주석 처리된
> `<Reference>` 블록을 활성화하고 NuGet 패키지를 제거해도 됩니다.

---

## AutoCAD에 로드하기

AutoCAD 명령창에서:

```
NETLOAD
```

→ 빌드된 `CivilRun_MadeByHouuu.dll`을 선택합니다.

매번 수동 로드가 번거로울 경우 `acadappobj` 또는 `APPLOAD` 시작 스위트에 등록하거나,
`startup.lsp`를 사용하세요.

---

## 팔레트 열기

```
DP
```

`DP` 명령어를 실행하면 사이드 팔레트가 열립니다. 각 버튼을 클릭하면 해당 명령이 실행됩니다.

---

## 제공 명령어

### 레이어 관리
| 명령어 | 설명 |
|---|---|
| `DP_LAYER_NEW` | 레이어 일괄 생성 (쉼표 구분) |
| `DP_LAYER_COLOR` | 레이어 색상 변경 |
| `DP_LAYER_FREEZE_OTHERS` | 현재 레이어 외 모두 동결 |
| `DP_LAYER_THAW_ALL` | 모든 레이어 동결 해제 |
| `DP_LAYER_LIST` | 레이어 목록 및 상태 출력 |
| `DP_LAYER_DELETE_EMPTY` | 사용되지 않는 빈 레이어 삭제 |

### 블록 / 도면 관리
| 명령어 | 설명 |
|---|---|
| `DP_BLOCK_INSERT` | 외부 DWG를 블록으로 삽입 |
| `DP_BLOCK_LIST` | 도면 내 블록 목록 출력 |
| `DP_BLOCK_COUNT` | 특정 블록 개수 세기 |
| `DP_BLOCK_EXPLODE_ALL` | 선택 블록 일괄 분해 |
| `DP_BLOCK_REPLACE` | 블록 이름 일괄 교체 |

### 문자 / 치수 도구
| 명령어 | 설명 |
|---|---|
| `DP_TEXT_HEIGHT` | 선택 텍스트 높이 일괄 변경 |
| `DP_TEXT_STYLE` | 선택 텍스트 스타일 일괄 변경 |
| `DP_TEXT_LAYER` | 선택 텍스트 레이어 이동 |
| `DP_DIM_STYLE` | 치수 스타일 일괄 변경 |
| `DP_DIM_SCALE` | 치수 전체 축척(DIMSCALE) 변경 |

### 내보내기 / 배치
| 명령어 | 설명 |
|---|---|
| `DP_PLOT_PDF` | 모든 레이아웃을 PDF로 일괄 출력 |
| `DP_EXPORT_DXF` | 도면을 DXF로 내보내기 |
| `DP_LAYOUT_NEW` | 새 레이아웃 생성 |
| `DP_LAYOUT_COPY` | 레이아웃 복사 |
| `DP_LAYOUT_LIST` | 레이아웃 목록 출력 |

---

## 프로젝트 구조

```
CivilRun_MadeByHouuu/
├── CivilRun_MadeByHouuu.csproj
├── CivilRun_MadeByHouuuApp.cs          # 플러그인 진입점 (IExtensionApplication)
├── Commands/
│   ├── PaletteCommands.cs   # DP 팔레트 토글
│   ├── LayerCommands.cs     # 레이어 관련 명령
│   ├── BlockCommands.cs     # 블록/도면 관련 명령
│   ├── TextCommands.cs      # 문자/치수 관련 명령
│   └── ExportCommands.cs    # PDF/DXF 내보내기, 레이아웃 관리
├── Helpers/
│   └── LayerHelper.cs       # 공통 유틸리티
└── UI/
    ├── CivilRun_MadeByHouuuPalette.xaml    # WPF 팔레트 UI
    └── CivilRun_MadeByHouuuPalette.xaml.cs
```

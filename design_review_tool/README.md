# 설계도서 검토 툴 — 기능 2: 수량산출서 ↔ SGS내역서 비교

여러 공종별 수량산출서 엑셀 파일과 SGS내역서(예정가격 산정용)의 수량을
비교해 항목 누락과 수량 불일치를 검토자가 빠르게 확인하도록 돕는
반자동 검토 보조 도구입니다.

**완전 자동 판정을 목표로 하지 않습니다.** 명백한 것은 자동 확정하고,
애매한 것은 후보와 함께 "확인 필요"로 남겨 검토자가 최종 결정합니다.
설계 배경과 판정 기준은 원본 스펙 문서를 따릅니다.

## 설치

```bash
cd design_review_tool
pip install -e .          # 또는: pip install -r requirements.txt
```

## 사용법 — GUI (파일 추가 방식)

명령줄이 익숙하지 않다면 GUI를 쓰는 게 편합니다.

```bash
pip install -e .
python -m design_review_tool.gui.app
```

(설치 후에는 `qty-vs-sgs-gui` 명령으로도 실행할 수 있습니다.)

창에서 "찾아보기"로 SGS내역서를, "+ 파일 추가"로 수량산출서(여러 개 선택 가능)를
고르면 됩니다. 처음 보는 파일이면 시트 목록과 첫 5행을 보여주는 창이 뜨고
공종/규격/단위/수량 열 번호와 대분류를 입력하면 됩니다 — 입력한 내용은 저장되어
다음에 같은 이름 패턴의 파일을 추가할 때 자동으로 재사용됩니다. "비교 실행"을
누르면 아래 결과 영역에 등급별 탭(매칭(확정)/확인 필요/미매칭)으로 나뉘어
표시되고, "엑셀로 저장" 버튼으로 등급별 시트가 나뉜 .xlsx나 .csv로 내보낼 수
있습니다.

### 더블클릭 실행 파일(.exe)로 만들기 (Windows)

Windows에서 아래를 실행하면 `dist\설계도서검토툴.exe` 하나로 배포/실행할 수
있습니다 (PyInstaller가 필요한 라이브러리를 전부 포함해 하나의 파일로 묶어줍니다).

```powershell
cd design_review_tool
build_exe.bat
```

빌드된 `.exe`는 Python이 안 깔린 PC에서도 더블클릭만으로 실행됩니다. 컬럼 매핑
설정(새 파일 학습 결과)은 실행 파일 자체가 아니라 `%APPDATA%\DesignReviewTool\column_mappings.json`에
저장되므로, exe를 다시 빌드하거나 옮겨도 한 번 가르쳐둔 파일명 패턴은 계속
재사용됩니다.

## 사용법 — CLI

```bash
python -m design_review_tool.features.quantity_vs_sgs.cli \
  --sgs "SGS내역서.xls" \
  --qty "2-00_포장공수량집계.xls" "3-00_부대공수량집계.xls" \
  --output "결과.xlsx"
```

(설치 후에는 `qty-vs-sgs` 명령으로도 동일하게 실행할 수 있습니다.)

- `--qty`는 여러 파일을 받을 수 있습니다. 파일명이 `config/column_mappings.json`에
  등록된 키를 포함하면 컬럼 매핑(시트명/열 위치/대분류)이 자동 적용됩니다.
- 등록되지 않은 새 파일이면 시트 목록과 첫 5행을 보여주고 열 번호(공종/규격/단위/수량)와
  대분류를 CLI로 물어본 뒤, 이후 실행에서 재사용할 수 있도록 설정 파일에 저장합니다.
  (`--no-interactive`를 주면 프롬프트 대신 에러로 종료합니다.)
- 대분류를 파일별로 CLI에서 바로 지정하려면 `경로:대분류` 형식을 씁니다.
  예: `--qty "부대공수량집계.xls:부대공"`
- `--output`을 `.xlsx`로 주면 등급별(매칭(확정)/확인 필요(유사도 낮음)/
  확인 필요(수량차이 비정상)/미매칭(수량산출서에만 존재)/미매칭(SGS에만 존재))
  시트로 분리해 저장합니다. `.csv`로 주면 등급을 한 열로 포함한 단일 CSV로 저장합니다.
- 유사도/수량차이 임계값은 `--high-threshold`, `--low-threshold`, `--qty-diff-pct`로
  조정할 수 있습니다 (기본값 85 / 60 / 50, 실사용 데이터 검증 기준).

## 모듈 구조

```
src/design_review_tool/
├── common/            # normalize(텍스트 정규화), is_number(숫자 판별)
├── io/                # SheetReader: 확장자별(xls→xlrd, xlsx→openpyxl) 통일 인터페이스
├── parsers/
│   ├── models.py           # Item 데이터클래스
│   ├── sgs_parser.py       # SGS내역서 파싱 (대분류 행 추적 포함)
│   ├── quantity_parser.py  # 수량산출서 파싱 (고정폭 컬럼 매핑 기반)
│   └── column_mapping.py   # 파일명 패턴별 컬럼 매핑 설정 로드/저장/대화형 입력
├── matching/
│   └── engine.py       # MatchGrade/MatchConfig/MatchResult, match_items()
├── features/quantity_vs_sgs/
│   ├── cli.py           # CLI 실행 진입점
│   └── report.py        # 콘솔 출력 + CSV/엑셀 저장
├── gui/
│   ├── app.py               # Tkinter 메인 창 (파일 추가/비교 실행/결과 탭/엑셀 저장)
│   └── mapping_dialog.py    # 새 파일의 컬럼 매핑을 입력받는 모달 창
└── config/
    └── column_mappings.json  # 파일명 패턴 → {시트, 컬럼 위치, 대분류} 기본 설정
```

`matching/engine.py`는 특정 기능에 종속되지 않도록 설계했습니다. 스펙의
개발 순서 제안대로, 이후 기능 3(제비율표 비교) 착수 시 이 매칭 엔진을
그대로 재사용할 수 있습니다.

## 판정 등급

| 등급 | 조건 |
|---|---|
| 매칭(확정) | 유사도 ≥ high_threshold(기본 85) AND 수량차이 ≤ qty_diff_pct(기본 50%) |
| 확인 필요(수량차이 비정상) | 유사도는 높지만(≥85) 수량차이가 임계값을 초과 |
| 확인 필요(유사도 낮음) | 유사도가 low_threshold~high_threshold(기본 60~85) 사이 |
| 미매칭(수량산출서에만 존재) | 유사도 < low_threshold |
| 미매칭(SGS에만 존재) | 어떤 수량산출서 항목에도 확정 매칭되지 않은 SGS 잔여 항목 |

## 알려진 한계 (v1)

- 관급자재/도급자재처럼 수량산출서 한 항목이 SGS에서 여러 줄로 나뉘는
  1:다 관계는 지원하지 않습니다. 이런 경우 "확인 필요"로 노출만 됩니다.
- 수량 차이의 원인(할증률 적용 vs 단순 오류)은 판별하지 않습니다. 차이(%)만
  계산해서 보여주고 판단은 검토자 몫입니다.

## 테스트

```bash
pip install -e ".[dev]"
pytest
```

# CLAUDE.md — 건설공사 발주도서 검토 툴

AI 어시스턴트가 이 코드베이스를 이해하고 기여하기 위한 가이드입니다.

---

## 프로젝트 개요

건설공사 발주도서(설계도면·수량산출서·내역서)를 업로드하면 Claude Opus 4.6 AI가 문서 간 상호 비교 및 건설공사 표준품셈 기준 검토를 수행하는 웹 애플리케이션입니다.

**기술 스택**
- **백엔드**: FastAPI (Python) + Claude Opus 4.6 API
- **프론트엔드**: Next.js 16 (React 19) + TypeScript + Tailwind CSS v4
- **문서 파싱**: pdfplumber, pandas/openpyxl, ezdxf

---

## 개발 환경 실행

### 원클릭 실행

```bash
export ANTHROPIC_API_KEY=your_key_here
./start.sh
```

### 수동 실행

**백엔드** (포트 8000):
```bash
cd backend
python3 -m venv venv && source venv/bin/activate
pip install -r requirements.txt
export ANTHROPIC_API_KEY=your_key_here
uvicorn main:app --reload
```

**프론트엔드** (포트 3000):
```bash
cd frontend
npm install
cp .env.local.example .env.local
npm run dev
```

### 접속 URL
| URL | 용도 |
|-----|------|
| http://localhost:3000 | 웹 UI |
| http://localhost:8000/docs | FastAPI Swagger UI |
| http://localhost:8000/api/health | 헬스체크 |

---

## 프로젝트 구조

```
filter-tracker/
├── backend/
│   ├── main.py                   # FastAPI 앱, CORS, 라우터 등록
│   ├── requirements.txt
│   ├── .env.example              # ANTHROPIC_API_KEY 템플릿
│   ├── models/
│   │   └── schemas.py            # Pydantic 모델 (DocumentType, Issue, AnalysisResult 등)
│   ├── routers/
│   │   ├── documents.py          # 문서 업로드/조회/삭제 API
│   │   └── analysis.py           # 분석 실행/스트리밍/조회 API
│   └── services/
│       ├── document_parser.py    # PDF/Excel/CSV/DXF 파싱 로직
│       └── claude_analyzer.py   # Claude API 분석 엔진
├── frontend/
│   ├── app/
│   │   ├── layout.tsx            # 루트 레이아웃 (lang="ko")
│   │   ├── page.tsx              # 메인 페이지 (3단계 워크플로우)
│   │   └── globals.css           # Tailwind 임포트 + 테마
│   ├── components/
│   │   ├── DocumentUploadCard.tsx  # 드래그앤드롭 업로드 카드
│   │   └── AnalysisResults.tsx     # 분석 결과 대시보드
│   ├── lib/
│   │   └── api.ts                # 백엔드 API 클라이언트 + 타입 정의
│   └── .env.local.example        # NEXT_PUBLIC_API_URL 템플릿
└── start.sh                      # 백엔드 + 프론트엔드 동시 실행
```

---

## 핵심 아키텍처

### 분석 워크플로우

```
[파일 업로드]
    → POST /api/documents/upload (multipart/form-data)
    → document_parser.py 파싱 (PDF/Excel/CSV/DXF)
    → 인메모리 저장 (_documents dict)

[분석 실행]
    → POST /api/analysis/run (document_ids[], analysis_types[])
    → claude_analyzer.py: 프롬프트 구성 + Claude Opus 4.6 호출
    → streaming + adaptive thinking
    → JSON 응답 파싱 → AnalysisResult 반환

[결과 표시]
    → AnalysisResults.tsx: 이슈 카드, 비교 결과, 표준품셈 검토
```

### 문서 유형 (DocumentType)

| 값 | 한국어 | 지원 형식 |
|----|--------|-----------|
| `drawing` | 설계도면 | PDF, DXF, DWG |
| `quantity` | 수량산출서 | Excel(xlsx/xls), CSV, PDF |
| `boq` | 내역서 | Excel(xlsx/xls), CSV, PDF |

### 분석 유형 (analysis_types)

| 값 | 설명 |
|----|------|
| `cross_check` | 문서 간 수량·규격·단위 정합성 비교 |
| `standard_check` | 건설공사 표준품셈 기준 검토 |
| `quantity_check` | 수량 계산식·단위 오류 검토 |

---

## 백엔드 규칙

### API 엔드포인트 패턴

- 모든 라우트는 `/api/` 접두사 사용
- 라우터 파일: `routers/` 디렉토리, `APIRouter(prefix="/api/...")` 패턴
- 새 라우터는 `main.py`에 `app.include_router(...)` 등록 필요
- 에러 처리: `raise HTTPException(status_code=..., detail="한국어 메시지")`

### 데이터 모델

- `models/schemas.py`에 Pydantic 모델 통합 관리
- 모든 ID는 `str(uuid.uuid4())` 형식
- 상태 관리: `DocumentStatus` Enum (PENDING, PROCESSING, DONE, ERROR)
- 이슈 심각도: `IssueLevel` Enum (error, warning, info)

### 문서 파싱 (`services/document_parser.py`)

- `parse_document(file_path, doc_type, filename)` - 확장자 기반 디스패치
- 반환 구조: `{"type": ..., "text": ..., "tables": [...], "metadata": {...}}`
- `truncate_for_context(parsed_data, max_chars=15000)` - Claude 컨텍스트 제한 대응
- DWG는 ezdxf 직접 파싱 시도 후 실패 시 DXF 변환 안내

### Claude API 사용 (`services/claude_analyzer.py`)

- **모델**: `claude-opus-4-6` (변경 금지)
- **thinking**: `{"type": "adaptive"}` (budget_tokens 사용 금지)
- **streaming**: 장시간 요청이므로 반드시 스트리밍 사용
- `max_tokens`: 8000 (분석 결과 JSON이 길 수 있음)
- 응답 JSON 추출: markdown 코드 블록(`\`\`\`json`) 우선 파싱 후 raw JSON 시도
- 프롬프트는 `_build_analysis_prompt()`에서 조립, `SYSTEM_PROMPT`은 모듈 상수

```python
# 올바른 Claude 호출 패턴
with client.messages.stream(
    model="claude-opus-4-6",
    max_tokens=8000,
    thinking={"type": "adaptive"},  # adaptive thinking
    system=SYSTEM_PROMPT,
    messages=[{"role": "user", "content": prompt}],
) as stream:
    response = stream.get_final_message()
```

### 저장소

현재 인메모리 저장소 사용 (`_documents`, `_results` dict). 서버 재시작 시 초기화됨.
프로덕션 전환 시 PostgreSQL/SQLite 등 DB로 교체 필요.

---

## 프론트엔드 규칙

### 컴포넌트 패턴

- 페이지 컴포넌트: `app/` 디렉토리, `"use client"` 지시자 필요시 명시
- 재사용 컴포넌트: `components/` 디렉토리, `PascalCase.tsx`
- 이벤트 핸들러: `handleXxx` 네이밍 패턴
- 콜백은 부모에서 `useCallback`으로 메모이제이션 후 props으로 전달

### API 호출

- 모든 백엔드 통신은 `lib/api.ts` 함수 사용
- 타입은 `lib/api.ts`에서 import (backend 스키마와 동기화 필요)
- 에러 처리: `catch (e) { setError(e instanceof Error ? e.message : "fallback") }`
- 환경변수: `process.env.NEXT_PUBLIC_API_URL` (기본값 `http://localhost:8000`)

### 스타일링

- **Tailwind CSS v4** 사용 (v3 문법과 다름, `@theme` 사용)
- 색상 팔레트: 도면=blue, 수량산출서=green, 내역서=orange
- `colorMap` 객체로 색상 변형 관리 (`DocumentUploadCard.tsx` 참고)
- 반응형: `sm:`, `md:` 브레이크포인트 활용 (모바일 우선)

### 타입 정의

```typescript
// lib/api.ts 타입과 backend schemas.py 동기화 유지
type DocumentType = "drawing" | "quantity" | "boq";

interface Issue {
  level: "error" | "warning" | "info";
  category: string;
  // ... 백엔드 Issue 모델과 일치해야 함
}
```

---

## 네이밍 컨벤션

### Python (백엔드)
| 항목 | 규칙 | 예시 |
|------|------|------|
| 파일 | snake_case | `document_parser.py` |
| 클래스 | PascalCase | `UploadedDocument` |
| 함수 | snake_case | `parse_pdf()` |
| 상수 | UPPER_SNAKE_CASE | `SYSTEM_PROMPT`, `MAX_FILE_SIZE` |
| private 함수 | `_leading_underscore` | `_build_analysis_prompt()` |
| Enum 값 | UPPER_SNAKE_CASE | `DocumentType.DRAWING` |

### TypeScript (프론트엔드)
| 항목 | 규칙 | 예시 |
|------|------|------|
| 컴포넌트 파일 | PascalCase.tsx | `DocumentUploadCard.tsx` |
| 함수/변수 | camelCase | `handleUpload`, `analysisTypes` |
| 타입/인터페이스 | PascalCase | `AnalysisResult`, `Issue` |
| 배열 상수 | UPPER_SNAKE_CASE | `DOC_CONFIGS`, `ANALYSIS_OPTIONS` |
| 이벤트 핸들러 | `handle` 접두사 | `handleDelete`, `handleAnalyze` |

---

## 환경변수

| 변수 | 위치 | 필수 | 기본값 |
|------|------|------|--------|
| `ANTHROPIC_API_KEY` | backend/.env | ✅ | 없음 |
| `NEXT_PUBLIC_API_URL` | frontend/.env.local | ❌ | `http://localhost:8000` |

---

## 의존성 추가 시 주의사항

- **백엔드**: `backend/requirements.txt`에 버전 고정하여 추가
- **프론트엔드**: `npm install --save` (devDependency면 `--save-dev`)
- 한국어 CSV 처리 시 인코딩 순서: `utf-8-sig` → `cp949` → `utf-8`
- 대용량 PDF 처리 시 `truncate_for_context()` 호출 확인

---

## 확장 가이드

### 새 문서 형식 추가

1. `document_parser.py`의 `parse_document()` 분기에 새 확장자 추가
2. 전용 파서 함수 `parse_xxx()` 작성
3. `backend/main.py`의 `/api/supported-formats` 응답 업데이트
4. `frontend/lib/api.ts`의 acceptedFormats 문자열 업데이트

### 새 분석 유형 추가

1. `claude_analyzer.py`의 `_build_analysis_prompt()`에 분기 추가
2. `frontend/app/page.tsx`의 `ANALYSIS_OPTIONS` 배열에 항목 추가

### 영구 저장소 전환

- `routers/documents.py`의 `_documents` dict을 DB 쿼리로 교체
- `routers/analysis.py`의 `_results` dict을 DB 쿼리로 교체
- SQLAlchemy 또는 SQLModel 사용 권장

---

## Git 브랜치 컨벤션

```
claude/<feature-name>-<SESSION_ID>
예: claude/construction-doc-review-tool-POMdi
```

커밋 메시지:
```
feat: 한국어로 기능 설명
fix: 버그 수정 내용
refactor: 리팩토링 내용
```

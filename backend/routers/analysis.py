"""
문서 분석 라우터
"""
import uuid
import logging
from typing import List, Optional

from fastapi import APIRouter, HTTPException, BackgroundTasks
from fastapi.responses import StreamingResponse
import anthropic

from models.schemas import AnalysisRequest, AnalysisResult, Issue, IssueLevel
from services.claude_analyzer import analyze_documents, _build_analysis_prompt, SYSTEM_PROMPT
from routers.documents import get_document_content, _documents

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/analysis", tags=["analysis"])

# 분석 결과 저장
_results: dict[str, AnalysisResult] = {}


@router.post("/run", response_model=AnalysisResult)
async def run_analysis(request: AnalysisRequest):
    """발주도서 분석 실행"""
    if not request.document_ids:
        raise HTTPException(status_code=400, detail="분석할 문서를 선택하세요.")

    # 문서 내용 수집
    docs = []
    for doc_id in request.document_ids:
        content = get_document_content(doc_id)
        if not content:
            raise HTTPException(
                status_code=404, detail=f"문서 ID {doc_id}를 찾을 수 없거나 파싱되지 않았습니다."
            )
        docs.append(content)

    if len(docs) < 2:
        raise HTTPException(
            status_code=400, detail="비교 분석을 위해 최소 2개 이상의 문서가 필요합니다."
        )

    # 분석 실행
    try:
        result_data = analyze_documents(docs, request.analysis_types)
    except anthropic.AuthenticationError:
        raise HTTPException(status_code=401, detail="Claude API 키가 유효하지 않습니다. ANTHROPIC_API_KEY를 확인하세요.")
    except anthropic.RateLimitError:
        raise HTTPException(status_code=429, detail="API 사용 한도 초과. 잠시 후 다시 시도하세요.")
    except Exception as e:
        logger.error(f"분석 오류: {e}")
        raise HTTPException(status_code=500, detail=f"분석 중 오류가 발생했습니다: {str(e)}")

    # 결과 구성
    session_id = str(uuid.uuid4())
    issues = []
    for raw_issue in result_data.get("issues", []):
        try:
            issues.append(
                Issue(
                    level=IssueLevel(raw_issue.get("level", "info")),
                    category=raw_issue.get("category", "기타"),
                    description=raw_issue.get("description", ""),
                    source_doc=raw_issue.get("source_doc"),
                    target_doc=raw_issue.get("target_doc"),
                    detail=raw_issue.get("detail"),
                    recommendation=raw_issue.get("recommendation"),
                )
            )
        except Exception as e:
            logger.warning(f"이슈 파싱 실패: {e}")

    result = AnalysisResult(
        session_id=session_id,
        summary=result_data.get("summary", "분석 완료"),
        issues=issues,
        cross_comparison=result_data.get("cross_comparison"),
        standard_check=result_data.get("standard_check"),
        statistics=result_data.get("statistics", {
            "total_issues": len(issues),
            "errors": sum(1 for i in issues if i.level == IssueLevel.ERROR),
            "warnings": sum(1 for i in issues if i.level == IssueLevel.WARNING),
            "infos": sum(1 for i in issues if i.level == IssueLevel.INFO),
            "documents_reviewed": len(docs),
        }),
        raw_analysis=result_data.get("raw_analysis"),
    )

    _results[session_id] = result
    return result


@router.get("/stream")
async def stream_analysis(document_ids: str, analysis_types: str = "cross_check,standard_check,quantity_check"):
    """SSE 스트리밍으로 실시간 분석 진행"""
    doc_id_list = document_ids.split(",")
    analysis_type_list = analysis_types.split(",")

    docs = []
    for doc_id in doc_id_list:
        content = get_document_content(doc_id.strip())
        if content:
            docs.append(content)

    if not docs:
        raise HTTPException(status_code=400, detail="분석할 문서를 찾을 수 없습니다.")

    # 문서 유형별 분류
    drawing = next((d for d in docs if d["doc_type"] == "drawing"), None)
    quantity = next((d for d in docs if d["doc_type"] == "quantity"), None)
    boq = next((d for d in docs if d["doc_type"] == "boq"), None)

    prompt = _build_analysis_prompt(drawing, quantity, boq, analysis_type_list)

    async def generate():
        yield "data: {\"type\": \"start\", \"message\": \"분석을 시작합니다...\"}\n\n"

        client = anthropic.Anthropic()
        try:
            with client.messages.stream(
                model="claude-opus-4-6",
                max_tokens=8000,
                thinking={"type": "adaptive"},
                system=SYSTEM_PROMPT,
                messages=[{"role": "user", "content": prompt}],
            ) as stream:
                for text in stream.text_stream:
                    # JSON 이스케이프
                    escaped = text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n")
                    yield f'data: {{"type": "delta", "text": "{escaped}"}}\n\n'

            yield "data: {\"type\": \"done\"}\n\n"
        except Exception as e:
            yield f'data: {{"type": "error", "message": "{str(e)}"}}\n\n'

    return StreamingResponse(generate(), media_type="text/event-stream")


@router.get("/{session_id}", response_model=AnalysisResult)
async def get_result(session_id: str):
    """분석 결과 조회"""
    result = _results.get(session_id)
    if not result:
        raise HTTPException(status_code=404, detail="분석 결과를 찾을 수 없습니다.")
    return result


@router.get("/", response_model=List[AnalysisResult])
async def list_results():
    """분석 결과 목록"""
    return list(_results.values())

"""
문서 분석 라우터 (로컬 직접 비교)
"""
import uuid
import logging
from typing import List

from fastapi import APIRouter, HTTPException

from models.schemas import AnalysisRequest, AnalysisResult, Issue, IssueLevel
from services.document_comparator import compare_documents
from routers.documents import get_document_content

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/analysis", tags=["analysis"])

# 분석 결과 저장
_results: dict[str, AnalysisResult] = {}


@router.post("/run", response_model=AnalysisResult)
async def run_analysis(request: AnalysisRequest):
    """발주도서 직접 비교 분석 실행"""
    if not request.document_ids:
        raise HTTPException(status_code=400, detail="분석할 문서를 선택하세요.")

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

    try:
        result_data = compare_documents(docs, request.analysis_types)
    except Exception as e:
        logger.error(f"분석 오류: {e}")
        raise HTTPException(status_code=500, detail=f"분석 중 오류가 발생했습니다: {str(e)}")

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

    stats = result_data.get("statistics", {})
    result = AnalysisResult(
        session_id=session_id,
        summary=result_data.get("summary", "분석 완료"),
        issues=issues,
        cross_comparison=result_data.get("cross_comparison"),
        standard_check=result_data.get("standard_check"),
        statistics=stats or {
            "total_issues": len(issues),
            "errors": sum(1 for i in issues if i.level == IssueLevel.ERROR),
            "warnings": sum(1 for i in issues if i.level == IssueLevel.WARNING),
            "infos": sum(1 for i in issues if i.level == IssueLevel.INFO),
            "documents_reviewed": len(docs),
        },
        raw_analysis=None,
    )

    _results[session_id] = result
    return result


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

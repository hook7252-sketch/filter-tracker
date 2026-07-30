"""설계도서 검토 프로그램 CLI.

사용 예:
    export ANTHROPIC_API_KEY=sk-ant-...
    python -m design_review.cli --input 설계도서.pdf --output 검토결과.xlsx \
        --project-name "산양일반산업단지" --review-scope "PDF 236쪽 전체"
"""
from __future__ import annotations

import argparse
import datetime as dt
import sys
from pathlib import Path

from . import config
from .analyze import (
    extract_all_pages,
    finalize_page_reviews,
    load_extracts_cache,
    run_cross_analysis,
    save_json,
)
from .llm_client import LLMClient
from .pdf_extract import extract_pages
from .schemas import ProjectMeta
from .xlsx_builder import build_workbook


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description="설계도서 PDF를 검토하여 종합 엑셀 보고서를 생성합니다.")
    p.add_argument("--input", required=True, help="검토할 설계도서 PDF 경로")
    p.add_argument("--output", required=True, help="출력 엑셀 경로 (.xlsx)")
    p.add_argument("--project-name", default=None, help="프로젝트명 (미지정시 PDF 파일명 사용)")
    p.add_argument("--review-scope", default=None, help="검토범위 설명 (미지정시 자동 생성)")
    p.add_argument("--model", default=config.DEFAULT_MODEL, help=f"Claude 모델 (기본값: {config.DEFAULT_MODEL})")
    p.add_argument("--batch-size", type=int, default=config.DEFAULT_BATCH_SIZE, help="1차 추출 배치당 페이지 수")
    p.add_argument("--cache-dir", default=".design_review_cache", help="1차 추출 결과 캐시 디렉터리")
    p.add_argument("--no-cache", action="store_true", help="캐시를 사용하지 않고 항상 새로 추출")
    p.add_argument("--max-pages", type=int, default=None, help="테스트용: 앞쪽 N페이지만 처리")
    return p.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    pdf_path = Path(args.input)
    if not pdf_path.exists():
        print(f"입력 PDF를 찾을 수 없습니다: {pdf_path}", file=sys.stderr)
        return 1

    project_name = args.project_name or pdf_path.stem
    review_date = dt.date.today().isoformat()

    print(f"[1/4] PDF 추출 중: {pdf_path}")
    pages = extract_pages(pdf_path)
    if args.max_pages:
        pages = pages[: args.max_pages]
    print(f"  -> {len(pages)}쪽 로드 완료")

    review_scope = args.review_scope or f"PDF {len(pages)}쪽 전체"
    meta = ProjectMeta(
        project_name=project_name,
        source_filename=pdf_path.name,
        review_scope=review_scope,
        review_date=review_date,
    )

    client = LLMClient(model=args.model)
    project_context = (
        f"프로젝트명: {project_name}\n원본 파일: {pdf_path.name}\n검토범위: {review_scope}\n"
        "설계설명서·시방서·내역서·단가산출서·수량산출서·도면·구조계산서 등이 포함된 국내 토목/건축 "
        "설계도서입니다."
    )

    cache_path = Path(args.cache_dir) / f"{pdf_path.stem}_extracts.json"
    extracts = None if args.no_cache else load_extracts_cache(cache_path)
    if extracts and len(extracts) == len(pages):
        print(f"[2/4] 캐시된 1차 추출 결과 사용: {cache_path}")
    else:
        print("[2/4] 1차 페이지 추출 진행 중 (Claude API 호출)...")
        extracts = extract_all_pages(pages, client, batch_size=args.batch_size, project_context=project_context)
        cache_path.parent.mkdir(parents=True, exist_ok=True)
        save_json(extracts, cache_path)
        print(f"  -> 캐시 저장: {cache_path}")

    print("[3/4] 교차분석 진행 중 (정합성/구조검토/공사비영향/기준참고)...")
    analysis = run_cross_analysis(extracts, client, project_context=project_context)
    print(f"  -> 이슈 {len(analysis.issues)}건, 구조검토 {len(analysis.structural_rows)}건 도출")

    print("[3/4] 페이지별 최종 판정 확정 중...")
    reviews = finalize_page_reviews(extracts, analysis, client)

    print("[4/4] 엑셀 보고서 생성 중...")
    out_path = build_workbook(args.output, meta, reviews, analysis)
    print(f"완료: {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

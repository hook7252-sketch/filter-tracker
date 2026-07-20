from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Callable, Optional, Sequence

from rapidfuzz import fuzz, process

from design_review_tool.parsers.models import Item

# 한국어 건설 용어의 어순 변형(예: "아스팔트 절삭 후 덧씌우기" ↔ "절삭후아스팔트덧씌우기")에
# 대응하기 위해 세 스코어러 중 최댓값을 채택한다.
DEFAULT_SCORERS: tuple[Callable, ...] = (fuzz.ratio, fuzz.token_sort_ratio, fuzz.partial_ratio)


class MatchGrade(str, Enum):
    CONFIRMED = "매칭(확정)"
    NEEDS_REVIEW_LOW_SIMILARITY = "확인 필요(유사도 낮음)"
    NEEDS_REVIEW_QTY_MISMATCH = "확인 필요(수량차이 비정상)"
    UNMATCHED_SOURCE_ONLY = "미매칭(수량산출서에만 존재)"
    UNMATCHED_TARGET_ONLY = "미매칭(SGS에만 존재)"


@dataclass
class MatchConfig:
    high_threshold: float = 85.0
    low_threshold: float = 60.0
    qty_diff_alarm_pct: float = 50.0
    scorers: Sequence[Callable] = field(default_factory=lambda: DEFAULT_SCORERS)


@dataclass
class MatchResult:
    grade: MatchGrade
    similarity: Optional[float]
    source: Optional[Item]
    target: Optional[Item] = None
    qty_diff: Optional[float] = None
    qty_diff_pct: Optional[float] = None

    def to_dict(self) -> dict:
        return {
            "등급": self.grade.value,
            "유사도": self.similarity,
            "수량산출서_공종": self.source.공종 if self.source else "",
            "수량산출서_규격": self.source.규격 if self.source else "",
            "수량산출서_수량": self.source.수량 if self.source else None,
            "SGS_공종": self.target.공종 if self.target else "",
            "SGS_규격": self.target.규격 if self.target else "",
            "SGS_수량": self.target.수량 if self.target else None,
            "수량차이": self.qty_diff,
            "수량차이(%)": self.qty_diff_pct,
            "출처파일": self.source.출처파일 if self.source else "",
        }


def _best_match(text: str, candidates: Sequence[str], scorers: Sequence[Callable]) -> Optional[tuple[int, float]]:
    """candidates 중 text와 가장 유사한 (인덱스, 점수)를 여러 스코어러 중 최댓값으로 반환한다."""
    best: Optional[tuple[int, float]] = None
    for scorer in scorers:
        result = process.extractOne(text, candidates, scorer=scorer)
        if result is None:
            continue
        _, score, idx = result
        if best is None or score > best[1]:
            best = (idx, score)
    return best


def _qty_diff(source: Item, target: Item) -> tuple[float, Optional[float]]:
    qty_diff = target.수량 - source.수량
    qty_diff_pct = (qty_diff / source.수량 * 100) if source.수량 else None
    return qty_diff, qty_diff_pct


def match_items(
    source_items: Sequence[Item],
    target_items: Sequence[Item],
    config: Optional[MatchConfig] = None,
) -> list[MatchResult]:
    """
    source(수량산출서) 각 항목을 target(SGS) 중 같은 대분류 안에서만 매칭한다.
    같은 대분류의 후보가 없으면 전체를 후보로 삼되, 대분류 밖 매칭은 오매칭
    위험이 크므로 자동 확정 등급까지는 올라가지 않도록 호출부에서 대분류를
    맞춰 넣는 것을 전제로 한다.

    - 유사도 >= high_threshold AND 수량차이 <= qty_diff_alarm_pct: 매칭(확정)
    - 유사도 >= high_threshold인데 수량차이가 비정상적으로 크면: 확인 필요(수량차이 비정상)
      (partial_ratio가 짧은 항목명을 긴 항목명에 완전 포함되는 것으로 오판하는 사례 방지)
    - low_threshold <= 유사도 < high_threshold: 확인 필요(유사도 낮음)
    - 유사도 < low_threshold: 미매칭(수량산출서에만 존재)
    - 어떤 source에도 매칭되지 않은 target: 미매칭(SGS에만 존재)
    """
    config = config or MatchConfig()
    results: list[MatchResult] = []
    matched_target_idx: set[int] = set()

    for s in source_items:
        candidate_idx = [
            i for i, t in enumerate(target_items)
            if s.대분류 and s.대분류 in t.대분류
        ]
        if not candidate_idx:
            candidate_idx = list(range(len(target_items)))

        candidate_texts = [target_items[i].결합텍스트 for i in candidate_idx]
        best = _best_match(s.결합텍스트, candidate_texts, config.scorers) if candidate_texts else None

        if best is None:
            results.append(MatchResult(
                grade=MatchGrade.UNMATCHED_SOURCE_ONLY,
                similarity=0.0,
                source=s,
            ))
            continue

        local_idx, score = best
        idx = candidate_idx[local_idx]
        t = target_items[idx]
        qty_diff, qty_diff_pct = _qty_diff(s, t)
        qty_diff_pct_rounded = round(qty_diff_pct, 1) if qty_diff_pct is not None else None

        if score >= config.high_threshold:
            if qty_diff_pct is not None and abs(qty_diff_pct) > config.qty_diff_alarm_pct:
                results.append(MatchResult(
                    grade=MatchGrade.NEEDS_REVIEW_QTY_MISMATCH,
                    similarity=round(score, 1),
                    source=s,
                    target=t,
                    qty_diff=round(qty_diff, 3),
                    qty_diff_pct=qty_diff_pct_rounded,
                ))
                continue

            matched_target_idx.add(idx)
            results.append(MatchResult(
                grade=MatchGrade.CONFIRMED,
                similarity=round(score, 1),
                source=s,
                target=t,
                qty_diff=round(qty_diff, 3),
                qty_diff_pct=qty_diff_pct_rounded,
            ))
        elif score >= config.low_threshold:
            # 확인 필요 항목은 target을 '소비'하지 않는다 (다른 진짜 매칭을 막지 않도록).
            results.append(MatchResult(
                grade=MatchGrade.NEEDS_REVIEW_LOW_SIMILARITY,
                similarity=round(score, 1),
                source=s,
                target=t,
                qty_diff=round(qty_diff, 3),
                qty_diff_pct=qty_diff_pct_rounded,
            ))
        else:
            results.append(MatchResult(
                grade=MatchGrade.UNMATCHED_SOURCE_ONLY,
                similarity=round(score, 1),
                source=s,
            ))

    for i, t in enumerate(target_items):
        if i not in matched_target_idx:
            results.append(MatchResult(
                grade=MatchGrade.UNMATCHED_TARGET_ONLY,
                similarity=None,
                source=None,
                target=t,
            ))

    return results

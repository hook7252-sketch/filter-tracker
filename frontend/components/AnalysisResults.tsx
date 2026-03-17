"use client";

import { AlertCircle, AlertTriangle, Info, CheckCircle, XCircle } from "lucide-react";
import type { AnalysisResult, Issue } from "@/lib/api";

interface Props {
  result: AnalysisResult;
}

const levelConfig = {
  error: {
    icon: <XCircle className="w-4 h-4" />,
    bg: "bg-red-50 border-red-200",
    badge: "bg-red-100 text-red-700",
    iconColor: "text-red-500",
    label: "오류",
  },
  warning: {
    icon: <AlertTriangle className="w-4 h-4" />,
    bg: "bg-yellow-50 border-yellow-200",
    badge: "bg-yellow-100 text-yellow-700",
    iconColor: "text-yellow-500",
    label: "경고",
  },
  info: {
    icon: <Info className="w-4 h-4" />,
    bg: "bg-blue-50 border-blue-200",
    badge: "bg-blue-100 text-blue-700",
    iconColor: "text-blue-500",
    label: "정보",
  },
};

function IssueCard({ issue }: { issue: Issue }) {
  const cfg = levelConfig[issue.level] || levelConfig.info;
  return (
    <div className={`rounded-lg border p-4 ${cfg.bg}`}>
      <div className="flex items-start gap-3">
        <span className={`mt-0.5 flex-shrink-0 ${cfg.iconColor}`}>{cfg.icon}</span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${cfg.badge}`}>
              {cfg.label}
            </span>
            <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">
              {issue.category}
            </span>
            {issue.source_doc && (
              <span className="text-xs text-gray-500">{issue.source_doc}</span>
            )}
            {issue.target_doc && (
              <>
                <span className="text-xs text-gray-400">→</span>
                <span className="text-xs text-gray-500">{issue.target_doc}</span>
              </>
            )}
          </div>
          <p className="mt-1.5 text-sm font-medium text-gray-800">{issue.description}</p>
          {issue.detail && (
            <p className="mt-1 text-sm text-gray-600 whitespace-pre-wrap">{issue.detail}</p>
          )}
          {issue.recommendation && (
            <div className="mt-2 flex items-start gap-1.5">
              <span className="text-xs text-gray-400 flex-shrink-0 mt-0.5">권고:</span>
              <p className="text-xs text-gray-600">{issue.recommendation}</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function ComparisonSection({ result }: { result: AnalysisResult }) {
  const cc = result.cross_comparison;
  if (!cc) return null;

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-5">
      <h3 className="font-semibold text-gray-800 mb-4">문서 간 비교 결과</h3>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        {cc.drawing_vs_quantity && (
          <div className="rounded-lg border border-gray-200 p-4">
            <div className="flex items-center justify-between mb-2">
              <span className="text-sm font-medium text-gray-700">설계도면 ↔ 수량산출서</span>
              {cc.drawing_vs_quantity.consistent ? (
                <span className="flex items-center gap-1 text-xs text-green-600">
                  <CheckCircle className="w-3.5 h-3.5" /> 일치
                </span>
              ) : (
                <span className="flex items-center gap-1 text-xs text-red-500">
                  <XCircle className="w-3.5 h-3.5" /> 불일치
                </span>
              )}
            </div>
            <p className="text-xs text-gray-500">
              검토 항목: {cc.drawing_vs_quantity.items_checked}개 /
              불일치: {(cc.drawing_vs_quantity.mismatches || []).length}건
            </p>
          </div>
        )}
        {cc.quantity_vs_boq && (
          <div className="rounded-lg border border-gray-200 p-4">
            <div className="flex items-center justify-between mb-2">
              <span className="text-sm font-medium text-gray-700">수량산출서 ↔ 내역서</span>
              {cc.quantity_vs_boq.consistent ? (
                <span className="flex items-center gap-1 text-xs text-green-600">
                  <CheckCircle className="w-3.5 h-3.5" /> 일치
                </span>
              ) : (
                <span className="flex items-center gap-1 text-xs text-red-500">
                  <XCircle className="w-3.5 h-3.5" /> 불일치
                </span>
              )}
            </div>
            <p className="text-xs text-gray-500">
              검토 항목: {cc.quantity_vs_boq.items_checked}개 /
              불일치: {(cc.quantity_vs_boq.mismatches || []).length}건
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

function StandardCheckSection({ result }: { result: AnalysisResult }) {
  const sc = result.standard_check;
  if (!sc) return null;

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-5">
      <h3 className="font-semibold text-gray-800 mb-4">표준품셈 기준 검토</h3>
      <div className="flex items-center gap-2 mb-3">
        {sc.compliant ? (
          <span className="flex items-center gap-1.5 text-sm text-green-600 font-medium">
            <CheckCircle className="w-4 h-4" /> 표준품셈 기준 준수
          </span>
        ) : (
          <span className="flex items-center gap-1.5 text-sm text-red-500 font-medium">
            <AlertCircle className="w-4 h-4" /> 기준 위반 항목 있음
          </span>
        )}
        {(sc.violations || []).length > 0 && (
          <span className="text-xs bg-red-100 text-red-600 px-2 py-0.5 rounded-full">
            위반 {sc.violations.length}건
          </span>
        )}
      </div>
      {sc.notes && sc.notes.length > 0 && (
        <ul className="space-y-1">
          {sc.notes.map((note, i) => (
            <li key={i} className="text-sm text-gray-600 flex items-start gap-1.5">
              <span className="text-gray-300 mt-1">•</span>
              {note}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default function AnalysisResults({ result }: Props) {
  const stats = result.statistics;
  const errorIssues = result.issues.filter((i) => i.level === "error");
  const warnIssues = result.issues.filter((i) => i.level === "warning");
  const infoIssues = result.issues.filter((i) => i.level === "info");

  return (
    <div className="space-y-6">
      {/* 요약 카드 */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-5">
        <h2 className="font-semibold text-gray-800 text-lg mb-3">검토 결과 요약</h2>
        <p className="text-gray-600 text-sm leading-relaxed">{result.summary}</p>

        {stats && (
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mt-4">
            <div className="text-center p-3 bg-gray-50 rounded-lg">
              <p className="text-2xl font-bold text-gray-800">{stats.total_issues}</p>
              <p className="text-xs text-gray-500 mt-0.5">전체 이슈</p>
            </div>
            <div className="text-center p-3 bg-red-50 rounded-lg">
              <p className="text-2xl font-bold text-red-600">{stats.errors}</p>
              <p className="text-xs text-red-500 mt-0.5">오류</p>
            </div>
            <div className="text-center p-3 bg-yellow-50 rounded-lg">
              <p className="text-2xl font-bold text-yellow-600">{stats.warnings}</p>
              <p className="text-xs text-yellow-500 mt-0.5">경고</p>
            </div>
            <div className="text-center p-3 bg-blue-50 rounded-lg">
              <p className="text-2xl font-bold text-blue-600">{stats.infos}</p>
              <p className="text-xs text-blue-500 mt-0.5">정보</p>
            </div>
          </div>
        )}
      </div>

      {/* 상호 비교 */}
      <ComparisonSection result={result} />

      {/* 표준품셈 */}
      <StandardCheckSection result={result} />

      {/* 이슈 목록 */}
      {result.issues.length > 0 && (
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-5">
          <h3 className="font-semibold text-gray-800 mb-4">상세 이슈 목록</h3>
          <div className="space-y-3">
            {errorIssues.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-red-500 uppercase tracking-wide mb-2">
                  오류 ({errorIssues.length})
                </p>
                <div className="space-y-2">
                  {errorIssues.map((issue, i) => (
                    <IssueCard key={i} issue={issue} />
                  ))}
                </div>
              </div>
            )}
            {warnIssues.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-yellow-500 uppercase tracking-wide mb-2 mt-4">
                  경고 ({warnIssues.length})
                </p>
                <div className="space-y-2">
                  {warnIssues.map((issue, i) => (
                    <IssueCard key={i} issue={issue} />
                  ))}
                </div>
              </div>
            )}
            {infoIssues.length > 0 && (
              <div>
                <p className="text-xs font-semibold text-blue-500 uppercase tracking-wide mb-2 mt-4">
                  참고사항 ({infoIssues.length})
                </p>
                <div className="space-y-2">
                  {infoIssues.map((issue, i) => (
                    <IssueCard key={i} issue={issue} />
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

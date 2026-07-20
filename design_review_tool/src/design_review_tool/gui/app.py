from __future__ import annotations

import tkinter as tk
from dataclasses import dataclass
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from design_review_tool.gui.mapping_dialog import ColumnMappingDialog
from design_review_tool.matching.engine import MatchConfig, MatchGrade, MatchResult, match_items
from design_review_tool.parsers.column_mapping import (
    DEFAULT_CONFIG_PATH,
    find_entry_for_file,
    load_config,
    save_config,
)
from design_review_tool.parsers.quantity_parser import parse_quantity_sheet
from design_review_tool.parsers.sgs_parser import parse_sgs
from design_review_tool.features.quantity_vs_sgs.report import save_report

EXCEL_FILETYPES = [("Excel 파일", "*.xls *.xlsx *.xlsm"), ("모든 파일", "*.*")]

RESULT_COLUMNS = (
    "유사도", "수량산출서_공종", "수량산출서_규격", "수량산출서_수량",
    "SGS_공종", "SGS_규격", "SGS_수량", "수량차이", "수량차이(%)", "출처파일",
)


@dataclass
class QtyFileEntry:
    path: str
    category: str
    sheet: str
    col_map: dict[str, int]


class App(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("설계도서 검토 툴 — 수량산출서 ↔ SGS내역서 비교")
        self.geometry("900x680")
        self.minsize(760, 560)

        self.sgs_path_var = tk.StringVar()
        self.sgs_sheet_var = tk.StringVar(value="내역서")
        self.high_var = tk.StringVar(value="85")
        self.low_var = tk.StringVar(value="60")
        self.qty_pct_var = tk.StringVar(value="50")
        self.status_var = tk.StringVar(
            value="SGS내역서와 수량산출서 파일을 추가한 뒤 '비교 실행'을 누르세요."
        )

        self.qty_files: list[QtyFileEntry] = []
        self.results: list[MatchResult] = []
        self.trees: dict[MatchGrade, ttk.Treeview] = {}

        self._build_widgets()

    # ---------- 위젯 구성 ----------

    def _build_widgets(self) -> None:
        top = ttk.Frame(self, padding=10)
        top.pack(fill="x")
        top.columnconfigure(1, weight=1)

        ttk.Label(top, text="SGS내역서:").grid(row=0, column=0, sticky="w")
        ttk.Entry(top, textvariable=self.sgs_path_var).grid(row=0, column=1, sticky="we", padx=8, pady=4)
        ttk.Button(top, text="찾아보기", command=self._browse_sgs).grid(row=0, column=2, pady=4)

        ttk.Label(top, text="SGS 시트명:").grid(row=1, column=0, sticky="w")
        ttk.Entry(top, textvariable=self.sgs_sheet_var, width=20).grid(row=1, column=1, sticky="w", padx=8, pady=4)

        mid = ttk.LabelFrame(self, text="수량산출서 목록", padding=10)
        mid.pack(fill="x", padx=10, pady=6)

        self.qty_listbox = tk.Listbox(mid, height=6)
        self.qty_listbox.pack(side="left", fill="both", expand=True)

        qty_btns = ttk.Frame(mid)
        qty_btns.pack(side="left", fill="y", padx=8)
        ttk.Button(qty_btns, text="+ 파일 추가", command=self._add_qty_files).pack(fill="x", pady=2)
        ttk.Button(qty_btns, text="- 선택 제거", command=self._remove_selected_qty).pack(fill="x", pady=2)

        run_frame = ttk.Frame(self, padding=(10, 0))
        run_frame.pack(fill="x")

        ttk.Label(run_frame, text="유사도 임계값(확정):").grid(row=0, column=0, sticky="w")
        ttk.Entry(run_frame, textvariable=self.high_var, width=6).grid(row=0, column=1, sticky="w", padx=(4, 12))
        ttk.Label(run_frame, text="유사도 임계값(확인필요):").grid(row=0, column=2, sticky="w")
        ttk.Entry(run_frame, textvariable=self.low_var, width=6).grid(row=0, column=3, sticky="w", padx=(4, 12))
        ttk.Label(run_frame, text="수량차이 임계값(%):").grid(row=0, column=4, sticky="w")
        ttk.Entry(run_frame, textvariable=self.qty_pct_var, width=6).grid(row=0, column=5, sticky="w", padx=(4, 0))

        ttk.Button(self, text="비교 실행", command=self._run).pack(pady=8)

        ttk.Label(self, textvariable=self.status_var, foreground="#555").pack(fill="x", padx=12)

        result_frame = ttk.LabelFrame(self, text="결과", padding=8)
        result_frame.pack(fill="both", expand=True, padx=10, pady=6)

        self.notebook = ttk.Notebook(result_frame)
        self.notebook.pack(fill="both", expand=True)

        for grade in MatchGrade:
            tab = ttk.Frame(self.notebook)
            self.notebook.add(tab, text=grade.value)
            self.trees[grade] = self._build_result_tree(tab)

        ttk.Button(self, text="엑셀로 저장", command=self._save_excel).pack(pady=8)

    def _build_result_tree(self, parent: tk.Misc) -> ttk.Treeview:
        tree = ttk.Treeview(parent, columns=RESULT_COLUMNS, show="headings", height=10)
        for col in RESULT_COLUMNS:
            tree.heading(col, text=col)
            tree.column(col, width=100, anchor="w", stretch=False)

        vsb = ttk.Scrollbar(parent, orient="vertical", command=tree.yview)
        hsb = ttk.Scrollbar(parent, orient="horizontal", command=tree.xview)
        tree.configure(yscrollcommand=vsb.set, xscrollcommand=hsb.set)

        tree.grid(row=0, column=0, sticky="nsew")
        vsb.grid(row=0, column=1, sticky="ns")
        hsb.grid(row=1, column=0, sticky="ew")
        parent.rowconfigure(0, weight=1)
        parent.columnconfigure(0, weight=1)
        return tree

    # ---------- 이벤트 핸들러 ----------

    def _browse_sgs(self) -> None:
        path = filedialog.askopenfilename(title="SGS내역서 선택", filetypes=EXCEL_FILETYPES)
        if path:
            self.sgs_path_var.set(path)

    def _add_qty_files(self) -> None:
        paths = filedialog.askopenfilenames(title="수량산출서 선택", filetypes=EXCEL_FILETYPES)
        for path in paths:
            entry = self._resolve_mapping_via_gui(path)
            if entry is None:
                continue  # 사용자가 매핑 입력을 취소함
            self.qty_files.append(QtyFileEntry(
                path=path, category=entry.category, sheet=entry.sheet, col_map=entry.col_map,
            ))
            self.qty_listbox.insert("end", f"{Path(path).name}   [{entry.category}]")

    def _resolve_mapping_via_gui(self, path: str):
        config = load_config(DEFAULT_CONFIG_PATH)
        entry = find_entry_for_file(Path(path).name, config)
        if entry is not None:
            return entry

        try:
            dialog = ColumnMappingDialog(self, path)
        except Exception as exc:
            messagebox.showerror("파일 열기 오류", f"{Path(path).name}을(를) 열 수 없습니다:\n{exc}")
            return None

        self.wait_window(dialog)
        if dialog.result is None:
            return None

        config[dialog.key] = dialog.result
        save_config(config, DEFAULT_CONFIG_PATH)
        return dialog.result

    def _remove_selected_qty(self) -> None:
        for idx in reversed(self.qty_listbox.curselection()):
            self.qty_listbox.delete(idx)
            del self.qty_files[idx]

    def _run(self) -> None:
        sgs_path = self.sgs_path_var.get().strip()
        if not sgs_path:
            messagebox.showerror("입력 오류", "SGS내역서 파일을 선택해주세요.")
            return
        if not self.qty_files:
            messagebox.showerror("입력 오류", "수량산출서 파일을 1개 이상 추가해주세요.")
            return

        try:
            high = float(self.high_var.get())
            low = float(self.low_var.get())
            qty_pct = float(self.qty_pct_var.get())
        except ValueError:
            messagebox.showerror("입력 오류", "임계값은 숫자로 입력해주세요.")
            return

        try:
            self.status_var.set("파싱 중...")
            self.update_idletasks()

            sgs_items = parse_sgs(sgs_path, sheet_name=self.sgs_sheet_var.get().strip() or "내역서")

            all_qty_items = []
            for qf in self.qty_files:
                all_qty_items.extend(parse_quantity_sheet(qf.path, qf.sheet, qf.col_map, qf.category))

            match_config = MatchConfig(high_threshold=high, low_threshold=low, qty_diff_alarm_pct=qty_pct)
            self.results = match_items(all_qty_items, sgs_items, match_config)

            self._populate_results()
            self.status_var.set(
                f"완료 — SGS {len(sgs_items)}건, 수량산출서 {len(all_qty_items)}건 파싱 · "
                f"결과 {len(self.results)}건"
            )
        except Exception as exc:
            messagebox.showerror("오류", str(exc))
            self.status_var.set("오류가 발생했습니다.")

    def _populate_results(self) -> None:
        for tree in self.trees.values():
            tree.delete(*tree.get_children())

        for r in self.results:
            d = r.to_dict()
            values = tuple(d[col] if d[col] is not None else "" for col in RESULT_COLUMNS)
            self.trees[r.grade].insert("", "end", values=values)

        for idx, grade in enumerate(MatchGrade):
            count = sum(1 for r in self.results if r.grade is grade)
            self.notebook.tab(idx, text=f"{grade.value} ({count})")

    def _save_excel(self) -> None:
        if not self.results:
            messagebox.showwarning("저장할 결과 없음", "먼저 '비교 실행'을 눌러 결과를 만들어주세요.")
            return
        path = filedialog.asksaveasfilename(
            title="결과 저장",
            defaultextension=".xlsx",
            filetypes=[("Excel 파일", "*.xlsx"), ("CSV 파일", "*.csv")],
        )
        if not path:
            return
        try:
            save_report(self.results, path)
        except Exception as exc:
            messagebox.showerror("저장 오류", str(exc))
            return
        messagebox.showinfo("저장 완료", f"결과를 저장했습니다:\n{path}")


def main() -> None:
    app = App()
    app.mainloop()


if __name__ == "__main__":
    main()

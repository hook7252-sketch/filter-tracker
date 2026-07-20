from __future__ import annotations

import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk
from typing import Optional

from design_review_tool.io.excel_reader import SheetReader
from design_review_tool.parsers.column_mapping import REQUIRED_FIELDS, ColumnMappingEntry


class ColumnMappingDialog(tk.Toplevel):
    """
    새 수량산출서 파일의 시트/컬럼 위치/대분류를 물어보는 모달 창.
    CLI의 prompt_for_mapping()과 동일한 정보를 GUI로 입력받는다.
    확인을 누르면 self.result(ColumnMappingEntry)와 self.key가 채워지고,
    취소하면 둘 다 None으로 남는다.
    """

    def __init__(self, parent: tk.Misc, path: str):
        super().__init__(parent)
        self.title(f"컬럼 매핑 설정 - {Path(path).name}")
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        self.path = path
        self.result: Optional[ColumnMappingEntry] = None
        self.key: Optional[str] = None

        self.reader = SheetReader(path)
        sheet_names = self.reader.sheet_names

        frm = ttk.Frame(self, padding=12)
        frm.pack(fill="both", expand=True)

        ttk.Label(frm, text=f"저장된 컬럼 매핑 설정이 없습니다: {Path(path).name}").grid(
            row=0, column=0, columnspan=4, sticky="w", pady=(0, 8)
        )

        ttk.Label(frm, text="시트:").grid(row=1, column=0, sticky="w")
        self.sheet_var = tk.StringVar(value=sheet_names[0])
        sheet_combo = ttk.Combobox(
            frm, textvariable=self.sheet_var, values=sheet_names, state="readonly", width=32
        )
        sheet_combo.grid(row=1, column=1, columnspan=3, sticky="w", pady=2)
        sheet_combo.bind("<<ComboboxSelected>>", lambda _e: self._refresh_preview())

        ttk.Label(frm, text="첫 5행 미리보기 (열 번호는 0부터 시작):").grid(
            row=2, column=0, columnspan=4, sticky="w", pady=(8, 2)
        )
        self.preview = tk.Text(frm, width=78, height=6, state="disabled", font=("Consolas", 9), wrap="none")
        self.preview.grid(row=3, column=0, columnspan=4, pady=2)

        self.col_vars: dict[str, tk.StringVar] = {}
        for i, field_name in enumerate(REQUIRED_FIELDS):
            ttk.Label(frm, text=f"'{field_name}' 열 번호:").grid(row=4 + i, column=0, sticky="w", pady=2)
            var = tk.StringVar()
            ttk.Entry(frm, textvariable=var, width=8).grid(row=4 + i, column=1, sticky="w")
            self.col_vars[field_name] = var

        next_row = 4 + len(REQUIRED_FIELDS)
        ttk.Label(frm, text="대분류(예: 포장공):").grid(row=next_row, column=0, sticky="w", pady=(8, 2))
        self.category_var = tk.StringVar()
        ttk.Entry(frm, textvariable=self.category_var, width=24).grid(
            row=next_row, column=1, columnspan=2, sticky="w", pady=(8, 2)
        )

        ttk.Label(frm, text="재사용 키(파일명 패턴):").grid(row=next_row + 1, column=0, sticky="w", pady=2)
        self.key_var = tk.StringVar(value=Path(path).stem)
        ttk.Entry(frm, textvariable=self.key_var, width=36).grid(
            row=next_row + 1, column=1, columnspan=3, sticky="w", pady=2
        )

        btn_frame = ttk.Frame(frm)
        btn_frame.grid(row=next_row + 2, column=0, columnspan=4, pady=(12, 0))
        ttk.Button(btn_frame, text="확인", command=self._on_confirm).pack(side="left", padx=4)
        ttk.Button(btn_frame, text="취소", command=self.destroy).pack(side="left", padx=4)

        self._refresh_preview()

    def _refresh_preview(self) -> None:
        rows = self.reader.preview_rows(self.sheet_var.get(), n=5)
        self.preview.config(state="normal")
        self.preview.delete("1.0", "end")
        for i, row in enumerate(rows):
            self.preview.insert("end", f"행{i}: {list(row)}\n")
        self.preview.config(state="disabled")

    def _on_confirm(self) -> None:
        try:
            col_map = {field_name: int(var.get()) for field_name, var in self.col_vars.items()}
        except ValueError:
            messagebox.showerror("입력 오류", "열 번호는 숫자로 입력해주세요.", parent=self)
            return

        category = self.category_var.get().strip()
        if not category:
            messagebox.showerror("입력 오류", "대분류를 입력해주세요.", parent=self)
            return

        key = self.key_var.get().strip() or Path(self.path).stem

        self.result = ColumnMappingEntry(sheet=self.sheet_var.get(), col_map=col_map, category=category)
        self.key = key
        self.destroy()

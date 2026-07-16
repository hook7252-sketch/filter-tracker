import openpyxl

from design_review_tool.parsers.column_mapping import (
    ColumnMappingEntry,
    load_config,
    resolve_column_mapping,
    save_config,
)


def test_resolve_uses_existing_config_by_filename_substring(tmp_path):
    config_path = tmp_path / "column_mappings.json"
    save_config({
        "2-00_포장공수량집계": ColumnMappingEntry(
            sheet="포장공내역서적용수량",
            col_map={"공종": 0, "규격": 15, "단위": 25, "수량": 30},
            category="포장공",
        )
    }, config_path)

    entry = resolve_column_mapping(
        "/data/2-00_포장공수량집계_장림여중.xlsx",
        config_path=config_path,
        interactive=False,
    )

    assert entry.sheet == "포장공내역서적용수량"
    assert entry.category == "포장공"
    assert entry.col_map == {"공종": 0, "규격": 15, "단위": 25, "수량": 30}


def test_resolve_raises_when_not_interactive_and_unknown(tmp_path):
    config_path = tmp_path / "column_mappings.json"
    save_config({}, config_path)

    try:
        resolve_column_mapping(
            "/data/처음보는파일.xlsx", config_path=config_path, interactive=False,
        )
        assert False, "LookupError가 발생해야 한다"
    except LookupError:
        pass


def test_resolve_prompts_and_persists_when_missing(tmp_path):
    config_path = tmp_path / "column_mappings.json"
    xlsx_path = tmp_path / "새파일.xlsx"
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "Sheet1"
    ws.append(["공종", "규격", "단위", "수량"])
    wb.save(xlsx_path)

    answers = iter(["Sheet1", "0", "1", "2", "3", "부대공", "새파일키"])
    entry = resolve_column_mapping(
        str(xlsx_path),
        config_path=config_path,
        interactive=True,
        input_func=lambda _: next(answers),
        print_func=lambda *a, **k: None,
    )

    assert entry.category == "부대공"
    assert entry.col_map == {"공종": 0, "규격": 1, "단위": 2, "수량": 3}

    saved = load_config(config_path)
    assert "새파일키" in saved
    assert saved["새파일키"].sheet == "Sheet1"


def test_category_override_replaces_configured_category(tmp_path):
    config_path = tmp_path / "column_mappings.json"
    save_config({
        "known": ColumnMappingEntry(
            sheet="s", col_map={"공종": 0, "규격": 1, "단위": 2, "수량": 3}, category="포장공",
        )
    }, config_path)

    entry = resolve_column_mapping(
        "/data/known_file.xlsx", config_path=config_path, interactive=False,
        category_override="부대공",
    )

    assert entry.category == "부대공"

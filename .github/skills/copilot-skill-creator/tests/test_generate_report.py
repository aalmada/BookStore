"""Tests for scripts/generate_report.py."""

import html as html_module

import pytest

from scripts.generate_report import generate_html


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _minimal_data(**overrides):
    base = {
        "original_description": "original desc",
        "best_description": "best desc",
        "best_score": "2/2",
        "iterations_run": 1,
        "holdout": 0.0,
        "train_size": 2,
        "test_size": 0,
        "history": [
            {
                "iteration": 1,
                "description": "best desc",
                "train_passed": 2,
                "train_failed": 0,
                "train_total": 2,
                "train_results": [
                    {"query": "add a book", "should_trigger": True, "pass": True, "triggers": 3, "runs": 3},
                    {"query": "list books", "should_trigger": False, "pass": True, "triggers": 0, "runs": 3},
                ],
                "test_passed": None,
                "test_failed": None,
                "test_total": None,
                "test_results": None,
                # backward-compat keys
                "passed": 2,
                "failed": 0,
                "total": 2,
                "results": [],
            }
        ],
    }
    base.update(overrides)
    return base


def _data_with_test_set():
    return {
        "original_description": "original",
        "best_description": "improved",
        "best_score": "1/1",
        "iterations_run": 1,
        "holdout": 0.4,
        "train_size": 1,
        "test_size": 1,
        "history": [
            {
                "iteration": 1,
                "description": "improved",
                "train_passed": 1,
                "train_failed": 0,
                "train_total": 1,
                "train_results": [
                    {"query": "train query", "should_trigger": True, "pass": True, "triggers": 2, "runs": 3},
                ],
                "test_passed": 1,
                "test_failed": 0,
                "test_total": 1,
                "test_results": [
                    {"query": "test query", "should_trigger": False, "pass": True, "triggers": 0, "runs": 3},
                ],
                "passed": 1,
                "failed": 0,
                "total": 1,
                "results": [],
            }
        ],
    }


# ---------------------------------------------------------------------------
# Basic structure
# ---------------------------------------------------------------------------

class TestGenerateHtmlStructure:
    def test_returns_valid_html_doctype(self):
        out = generate_html(_minimal_data())
        assert out.startswith("<!DOCTYPE html>")

    def test_contains_html_tag(self):
        out = generate_html(_minimal_data())
        assert "<html>" in out

    def test_contains_closing_body(self):
        out = generate_html(_minimal_data())
        assert "</body>" in out and "</html>" in out

    def test_no_auto_refresh_by_default(self):
        out = generate_html(_minimal_data())
        assert 'http-equiv="refresh"' not in out

    def test_auto_refresh_adds_meta_tag(self):
        out = generate_html(_minimal_data(), auto_refresh=True)
        assert 'http-equiv="refresh"' in out

    def test_title_contains_default_heading_when_no_skill_name(self):
        out = generate_html(_minimal_data())
        assert "Skill Description Optimization" in out

    def test_title_includes_skill_name_when_provided(self):
        out = generate_html(_minimal_data(), skill_name="MySkill")
        assert "MySkill" in out

    def test_skill_name_is_html_escaped(self):
        out = generate_html(_minimal_data(), skill_name="<Evil>")
        assert "<Evil>" not in out
        assert "&lt;Evil&gt;" in out


# ---------------------------------------------------------------------------
# Summary section
# ---------------------------------------------------------------------------

class TestGenerateHtmlSummary:
    def test_original_description_in_summary(self):
        out = generate_html(_minimal_data())
        assert "original desc" in out

    def test_best_description_in_summary(self):
        out = generate_html(_minimal_data())
        assert "best desc" in out

    def test_descriptions_are_html_escaped(self):
        data = _minimal_data(
            original_description='<script>alert("xss")</script>',
            best_description='<b>bold</b>',
        )
        out = generate_html(data)
        assert '<script>' not in out
        assert '&lt;script&gt;' in out
        assert '<b>bold</b>' not in out
        assert '&lt;b&gt;bold&lt;/b&gt;' in out


# ---------------------------------------------------------------------------
# Train/test query columns
# ---------------------------------------------------------------------------

class TestGenerateHtmlQueryColumns:
    def test_train_query_appears_as_column_header(self):
        out = generate_html(_minimal_data())
        assert "add a book" in out
        assert "list books" in out

    def test_test_query_appears_as_column_header(self):
        out = generate_html(_data_with_test_set())
        assert "train query" in out
        assert "test query" in out

    def test_test_columns_get_test_col_class(self):
        out = generate_html(_data_with_test_set())
        # test query header should be inside a th with class "test-col"
        assert 'test-col' in out

    def test_should_trigger_column_gets_positive_class(self):
        out = generate_html(_minimal_data())
        assert "positive-col" in out

    def test_should_not_trigger_column_gets_negative_class(self):
        out = generate_html(_minimal_data())
        assert "negative-col" in out


# ---------------------------------------------------------------------------
# Iteration rows
# ---------------------------------------------------------------------------

class TestGenerateHtmlIterationRows:
    def test_iteration_number_in_row(self):
        out = generate_html(_minimal_data())
        # iteration 1 should be in a <td>
        assert "<td>1</td>" in out

    def test_description_in_row_cell(self):
        out = generate_html(_minimal_data())
        assert "best desc" in out

    def test_description_is_html_escaped_in_row(self):
        data = _minimal_data()
        data["history"][0]["description"] = "<b>bold desc</b>"
        out = generate_html(data)
        assert "<b>bold desc</b>" not in out
        assert "&lt;b&gt;bold desc&lt;/b&gt;" in out

    def test_pass_result_shows_checkmark(self):
        out = generate_html(_minimal_data())
        assert "✓" in out

    def test_fail_result_shows_cross(self):
        data = _minimal_data()
        data["history"][0]["train_results"][0]["pass"] = False
        data["history"][0]["train_results"][0]["triggers"] = 0
        out = generate_html(data)
        assert "✗" in out

    def test_multiple_iterations_all_appear(self):
        data = _minimal_data()
        data["history"].append({
            "iteration": 2,
            "description": "second attempt",
            "train_passed": 1,
            "train_failed": 1,
            "train_total": 2,
            "train_results": [
                {"query": "add a book", "should_trigger": True, "pass": True, "triggers": 3, "runs": 3},
                {"query": "list books", "should_trigger": False, "pass": False, "triggers": 1, "runs": 3},
            ],
            "test_passed": None,
            "test_failed": None,
            "test_total": None,
            "test_results": None,
            "passed": 1,
            "failed": 1,
            "total": 2,
            "results": [],
        })
        out = generate_html(data)
        assert "<td>1</td>" in out
        assert "<td>2</td>" in out
        assert "second attempt" in out


# ---------------------------------------------------------------------------
# Edge cases
# ---------------------------------------------------------------------------

class TestGenerateHtmlEdgeCases:
    def test_empty_history_renders_without_error(self):
        data = _minimal_data(history=[])
        out = generate_html(data)
        assert "<!DOCTYPE html>" in out

    def test_query_text_html_escaped_in_column_header(self):
        data = _minimal_data()
        data["history"][0]["train_results"][0]["query"] = '<script>hack</script>'
        data["history"][0]["train_results"].pop(1)
        # rebuild minimal data so there's only the one query (avoid mismatch)
        data["train_size"] = 1
        out = generate_html(data)
        assert "<script>hack</script>" not in out

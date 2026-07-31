"""T021/T023 — the lake seam degrades LOUDLY to PGlite-only, never silently (R6)."""

from __future__ import annotations

import builtins

from ms_message.lake import DEGRADED_WARNING, Lake


class _StoreNeverCalled:
    def _rows(self, *a, **k):  # pragma: no cover - degradation must short-circuit first
        raise AssertionError("degraded lake must not touch the store")


def _broken_import(monkeypatch):
    real_import = builtins.__import__

    def fake(name, *args, **kwargs):
        if name == "duckdb":
            raise ImportError("duckdb deliberately unavailable (test)")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", fake)


def test_age_out_degrades_loudly_not_silently(tmp_path, monkeypatch, capsys):
    _broken_import(monkeypatch)
    lake = Lake(tmp_path / "lake")
    assert lake.age_out(_StoreNeverCalled()) is None  # honest None, never a fake 0-success
    assert lake.degraded
    err = capsys.readouterr().err
    assert DEGRADED_WARNING.split(" (")[0].split(":")[0] in err  # "LAKE DEGRADED"
    assert "duckdb" in err


def test_warning_prints_once_but_degradation_sticks(tmp_path, monkeypatch, capsys):
    _broken_import(monkeypatch)
    lake = Lake(tmp_path / "lake")
    assert lake.age_out(_StoreNeverCalled()) is None
    assert lake.age_out(_StoreNeverCalled()) is None
    err = capsys.readouterr().err
    assert err.count("LAKE DEGRADED") == 1  # loud once, not spam
    assert lake.degraded


def test_catchup_falls_back_to_hot_only(tmp_path, monkeypatch, capsys):
    _broken_import(monkeypatch)

    class HotStore:
        def _rows(self, sql, **params):
            return [{"sender_station": "a", "sender_seq": 1, "state": "fetched"}]

    lake = Lake(tmp_path / "lake")
    rows = lake.catchup_query(HotStore(), "news")
    assert rows == [{"sender_station": "a", "sender_seq": 1, "state": "fetched", "tier": "hot"}]

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import pyuff


def _basic(value: Any) -> Any:
    if value is None:
        return None

    if isinstance(value, (str, int, float, bool)):
        return value

    if hasattr(value, "item"):
        return value.item()

    return value


def _round_float(value: float | None) -> float | None:
    if value is None:
        return None

    return round(float(value), 6)


def _round_int(value: float | None) -> int | None:
    if value is None:
        return None

    return int(round(float(value)))


def _compute_spacing(x_values: Any) -> float | None:
    if x_values is None:
        return None

    try:
        x_list = [float(item) for item in x_values]
    except Exception:
        return None

    if len(x_list) < 2:
        return None

    diffs = [x_list[index + 1] - x_list[index] for index in range(len(x_list) - 1)]

    if not diffs or any(diff <= 0 for diff in diffs):
        return None

    baseline = diffs[0]
    tolerance = max(abs(baseline) * 0.001, 1e-9)

    if any(abs(diff - baseline) > tolerance for diff in diffs[1:]):
        return None

    return baseline


def _summarize_measurement(index: int, dataset: dict[str, Any]) -> dict[str, Any]:
    x_values = dataset.get("x")
    data_values = dataset.get("data")
    sample_count = len(data_values) if data_values is not None else 0
    spacing = _compute_spacing(x_values)
    axis_units = str(dataset.get("abscissa_axis_units_lab") or "").strip()
    ordinate_units = str(dataset.get("ordinate_axis_units_lab") or "").strip()

    sample_rate = None
    duration = None

    if spacing and axis_units.lower() in {"s", "sec", "second", "seconds"}:
        sample_rate = 1.0 / spacing
        duration = spacing * max(sample_count - 1, 0)

    return {
        "datasetIndex": index,
        "datasetType": int(dataset.get("type", 58)),
        "sampleCount": int(sample_count),
        "responseNode": _basic(dataset.get("rsp_node")),
        "responseDirection": _basic(dataset.get("rsp_dir")),
        "referenceNode": _basic(dataset.get("ref_node")),
        "referenceDirection": _basic(dataset.get("ref_dir")),
        "abscissaUnits": axis_units or None,
        "ordinateUnits": ordinate_units or None,
        "sampleRateHz": _round_int(sample_rate),
        "durationSeconds": _round_float(duration),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--file", required=True)
    args = parser.parse_args()

    file_path = Path(args.file).resolve()
    uff = pyuff.UFF(str(file_path))

    set_types = [int(item) for item in list(uff.get_set_types())]
    set_formats = [int(item) for item in list(uff.get_set_formats())]
    measurement_indexes = [index for index, set_type in enumerate(set_types) if set_type == 58]
    measurement_summaries = []

    for index in measurement_indexes:
        dataset = uff.read_sets(index)
        if isinstance(dataset, list):
            dataset = dataset[0]
        measurement_summaries.append(_summarize_measurement(index, dataset))

    first_measurement = measurement_summaries[0] if measurement_summaries else {}

    metadata = {
        "datasetCount": str(len(set_types)),
        "datasetTypes": json.dumps(set_types),
        "datasetFormats": json.dumps(set_formats),
        "measurementSetCount": str(len(measurement_summaries)),
        "measurementSummaries": json.dumps(measurement_summaries),
    }

    if first_measurement.get("abscissaUnits"):
        metadata["abscissaUnits"] = str(first_measurement["abscissaUnits"])

    if first_measurement.get("ordinateUnits"):
        metadata["ordinateUnits"] = str(first_measurement["ordinateUnits"])

    result = {
        "format": "uff",
        "signalKind": "engineering-signal",
        "channelCount": len(measurement_summaries) if measurement_summaries else None,
        "sampleRateHz": first_measurement.get("sampleRateHz"),
        "durationSeconds": first_measurement.get("durationSeconds"),
        "metadata": metadata,
    }

    print(json.dumps(result))


if __name__ == "__main__":
    main()

"""XGBoost undercut ranking model with heuristic fallback."""

from __future__ import annotations

import logging
from pathlib import Path

import numpy as np
import pandas as pd

from pentagon_analyzer.scoring.features import FEATURE_COLUMNS

logger = logging.getLogger(__name__)


class UndercutRanker:
    """Rank contracts by ease of undercut using XGBoost or heuristic fallback.

    Until labeled training data is available, uses a weighted heuristic:
        markup_ratio * 0.4 + competition * 0.3 +
        inverse_value * 0.2 + confidence * 0.1
    """

    def __init__(self, model_path: Path | None = None):
        self._model = None
        self._model_path = model_path
        if model_path and model_path.exists():
            self.load(model_path)

    @property
    def has_model(self) -> bool:
        return self._model is not None

    def train(self, features: pd.DataFrame, labels: pd.Series) -> dict:
        """Train XGBoost model on labeled data.

        Args:
            features: DataFrame with FEATURE_COLUMNS
            labels: Binary series (1 = successfully undercut, 0 = not)

        Returns:
            Training metrics dict.
        """
        try:
            import xgboost as xgb
            from sklearn.model_selection import cross_val_score
        except ImportError:
            logger.error("xgboost/sklearn not installed, cannot train")
            return {"error": "dependencies not installed"}

        X = features[FEATURE_COLUMNS].fillna(0)
        self._model = xgb.XGBClassifier(
            n_estimators=100,
            max_depth=6,
            learning_rate=0.1,
            objective="binary:logistic",
            eval_metric="auc",
        )
        self._model.fit(X, labels)

        scores = cross_val_score(self._model, X, labels, cv=5, scoring="roc_auc")
        metrics = {
            "mean_auc": float(np.mean(scores)),
            "std_auc": float(np.std(scores)),
            "n_samples": len(labels),
        }
        logger.info("Model trained: AUC=%.3f (+/- %.3f)", metrics["mean_auc"], metrics["std_auc"])
        return metrics

    def predict(self, features: pd.DataFrame) -> np.ndarray:
        """Return ease-of-undercut scores [0.0, 1.0] for each row.

        Falls back to heuristic if no trained model available.
        """
        if self._model is not None:
            X = features[FEATURE_COLUMNS].fillna(0)
            return self._model.predict_proba(X)[:, 1]

        # Heuristic fallback
        return np.array([self._heuristic_score(row) for _, row in features.iterrows()])

    def _heuristic_score(self, row: pd.Series) -> float:
        """Weighted heuristic scoring when no trained model available.

        Score = markup_ratio_norm * 0.4
              + competition_score * 0.3
              + inverse_contract_value * 0.2
              + match_confidence * 0.1
        """
        # Normalize markup ratio: cap at 100x for scoring
        markup = min(row.get("markup_ratio", 0), 100) / 100.0

        competition = row.get("competition_score", 0.5)

        # Inverse contract value: smaller contracts easier to undercut
        value_log = row.get("contract_value_log", 10)
        max_log = 25  # ~$70B
        inv_value = max(0, 1.0 - value_log / max_log)

        confidence = row.get("match_confidence", 0.5)

        score = markup * 0.4 + competition * 0.3 + inv_value * 0.2 + confidence * 0.1
        return max(0.0, min(1.0, score))

    def save(self, path: Path) -> None:
        """Save trained model to disk."""
        if self._model is None:
            raise ValueError("No trained model to save")
        self._model.save_model(str(path))
        logger.info("Model saved to %s", path)

    def load(self, path: Path) -> None:
        """Load trained model from disk."""
        try:
            import xgboost as xgb
            self._model = xgb.XGBClassifier()
            self._model.load_model(str(path))
            logger.info("Model loaded from %s", path)
        except Exception as exc:
            logger.warning("Failed to load model from %s: %s", path, exc)
            self._model = None

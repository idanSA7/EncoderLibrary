import sys
from pathlib import Path

# הוספת תיקיית השורש של הפרויקט ל-sys.path כדי שהייבוא של app יעבוד מכל מקום
project_root = Path(__file__).resolve().parent.parent
if str(project_root) not in sys.path:
    sys.path.insert(0, str(project_root))

import numpy as np
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation

from app.core.kriging.interpolator import OrdinaryKriging
from app.core.config import KrigingConfig

# יצירת קונפיגורציה ייעודית למפה (טווח השפעה a=35.0)
demo_config = KrigingConfig(default_range_a=35.0, default_sill_c=1.0)
model = OrdinaryKriging(config=demo_config)

# הגדרת גריד רציף לרקע
np.random.seed(101)
grid_res = 50
x_coords = np.linspace(0, 100, grid_res)
y_coords = np.linspace(0, 100, grid_res)
grid_x, grid_y = np.meshgrid(x_coords, y_coords)

# יצירת דגימות טיסות רנדומליות לחלוטין (מיקום ודלתות)
n_flights = 30
flight_coords = np.random.uniform(5, 95, size=(n_flights, 2))
flight_deltas = np.random.uniform(0.5, 9.0, size=n_flights)

# הגדרת ממשק גרפי
fig, ax = plt.subplots(figsize=(9, 8))
plt.subplots_adjust(right=0.84)

cmap = plt.cm.Spectral_r
min_val, max_val = 0.0, 10.0
levels = np.linspace(min_val, max_val, 16)

norm = plt.Normalize(vmin=min_val, vmax=max_val)
sm = plt.cm.ScalarMappable(cmap=cmap, norm=norm)
sm.set_array([])
cbar = fig.colorbar(sm, ax=ax, fraction=0.046, pad=0.04)
cbar.set_label("Estimated Delta (via Project Kriging)", fontsize=11, fontweight="bold")

latest_grid_prediction = np.zeros_like(grid_x)

# יצירת תווית Hover
tooltip = ax.annotate(
    "",
    xy=(0, 0),
    xytext=(15, 15),
    textcoords="offset points",
    bbox=dict(boxstyle="round,pad=0.5", fc="black", ec="white", lw=1, alpha=0.85),
    color="white",
    fontsize=9,
    fontweight="bold",
    zorder=20,
)
tooltip.set_visible(False)


def update(frame):
    global latest_grid_prediction
    ax.clear()

    current_count = max(3, frame + 1)
    active_coords = flight_coords[:current_count]
    active_deltas = flight_deltas[:current_count]

    # שימוש ישיר במחלקת הפרויקט: אימון ובניית מטריצת קריגינג
    model.fit(active_coords, active_deltas)

    # חישוב החיזוי לכל נקודה ברשת באמצעות predict של המחלקה
    prediction_grid = np.zeros((grid_res, grid_res))
    for r in range(grid_res):
        for c in range(grid_res):
            target = np.array([grid_x[r, c], grid_y[r, c]])
            pred_val, _ = model.predict(target)
            prediction_grid[r, c] = pred_val

    latest_grid_prediction = prediction_grid

    # ציור מפת החום וקווי הגובה
    ax.contourf(grid_x, grid_y, latest_grid_prediction, levels=levels, cmap=cmap, alpha=0.85)
    ax.contour(grid_x, grid_y, latest_grid_prediction, levels=levels, colors="gray", linewidths=0.35, alpha=0.45)

    # ציור הטיסות הדוגמות
    ax.scatter(
        active_coords[:, 0],
        active_coords[:, 1],
        c=active_deltas,
        cmap=cmap,
        edgecolors="black",
        linewidth=1.0,
        s=70,
        zorder=5,
        vmin=min_val,
        vmax=max_val,
    )

    # הדגשת הנקודה שנוספה אחרונה
    latest = active_coords[-1]
    ax.scatter(latest[0], latest[1], s=170, facecolors="none", edgecolors="white", linewidth=2.0, zorder=6)

    ax.set_title(
        f"Kriging Model in Action (Flights: {current_count}/{n_flights})",
        fontsize=12,
        fontweight="bold",
    )
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)
    ax.set_aspect("equal")
    ax.grid(True, linestyle="--", alpha=0.3)

    ax.add_artist(tooltip)


anim = FuncAnimation(fig, update, frames=n_flights, interval=200, repeat=False)


def on_hover(event):
    if event.inaxes != ax or event.xdata is None or event.ydata is None:
        if tooltip.get_visible():
            tooltip.set_visible(False)
            fig.canvas.draw_idle()
        return

    x_val = float(np.clip(event.xdata, 0, 100))
    y_val = float(np.clip(event.ydata, 0, 100))

    col_idx = int(np.clip(np.round((x_val / 100.0) * (grid_res - 1)), 0, grid_res - 1))
    row_idx = int(np.clip(np.round((y_val / 100.0) * (grid_res - 1)), 0, grid_res - 1))
    pred_val = latest_grid_prediction[row_idx, col_idx]

    tooltip.xy = (x_val, y_val)
    tooltip.set_text(f"X: {x_val:.1f}, Y: {y_val:.1f}\nDelta: {pred_val:.2f}")
    tooltip.set_visible(True)
    fig.canvas.draw_idle()


fig.canvas.mpl_connect("motion_notify_event", on_hover)

if __name__ == "__main__":
    plt.show()
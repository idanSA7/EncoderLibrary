import numpy as np
from app.core.mean_shift.clusterer import FlightMeanShiftClusterer
from app.core.mean_shift.config import MeanShiftConfig
"""


def run_test():
    #  [x, y, z, Temp, Pressure, Wind, Humidity, Delta]
    test_data = np.array([
        # אשכול 1: אזור טיסה מרכז, גובה 500, תנאים רגילים, דלתא נמוכה (~1.2)
        [32.101, 34.802, 500.0, 24.5, 1013.2, 5.1, 60.0, 1.21],
        [32.099, 34.798, 505.0, 24.7, 1013.0, 5.3, 59.5, 1.18],
        [32.103, 34.800, 498.0, 24.4, 1013.4, 4.9, 60.2, 1.25],
        [32.100, 34.805, 502.0, 24.6, 1013.1, 5.0, 60.0, 1.19],

        # אשכול 2: אזור טיסה דרום, גובה 1200, רוח חזקה, דלתא גבוהה (~4.5)
        [31.500, 34.700, 1200.0, 16.0, 890.5, 14.2, 40.0, 4.52],
        [31.505, 34.704, 1210.0, 15.8, 889.8, 14.5, 39.5, 4.48],
        [31.498, 34.697, 1195.0, 16.2, 891.0, 13.9, 40.8, 4.55],
        [31.502, 34.701, 1205.0, 15.9, 890.2, 14.0, 40.1, 4.45],

        # אשכול 3: אזור טיסה צפון, גובה 200, לח וחם, דלתא בינונית (~2.8)
        [32.850, 35.200, 200.0, 29.5, 1020.0, 2.0, 85.0, 2.82],
        [32.852, 35.198, 205.0, 29.7, 1019.8, 2.2, 84.5, 2.78],
        [32.848, 35.203, 195.0, 29.3, 1020.5, 1.9, 85.2, 2.85],

        # רעש חריג (Outlier) מבודד
        [33.500, 35.800, 800.0, 10.0, 950.0, 25.0, 20.0, 8.90]
    ])

    clusterer = FlightMeanShiftClusterer(config=MeanShiftConfig(bandwidth=0.8))
    centers = clusterer.fit_predict(test_data)

    print("scaled_centers results")
    print(centers)

if __name__ == "__main__":
    run_test()
"""
import sys
import numpy as np
from PyQt5.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout,
    QHBoxLayout, QPushButton, QLabel, QTextEdit, QFrame
)
from PyQt5.QtCore import QTimer
from matplotlib.backends.backend_qt5agg import FigureCanvasQTAgg as FigureCanvas
from matplotlib.figure import Figure
from scipy.interpolate import Rbf

# ייבוא המחלקה והקונפיגורציה שלך מהפרויקט
from app.core.mean_shift.clusterer import FlightMeanShiftClusterer
from app.core.mean_shift.config import MeanShiftConfig


class FlightPipelineVisualizer(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("VORTEX Navigation - Flight Mean Shift & Kriging Pipeline")
        self.setGeometry(100, 100, 1200, 780)

        # יצירת נתונים סינתטיים (8 ממדים: Lat, Lon, Alt, Temp, Pressure, Wind, Humidity, Delta)
        self.raw_data = self._generate_flight_data()
        self.clusterer = FlightMeanShiftClusterer(config=MeanShiftConfig(bandwidth=0.85))

        # שלבי ה-Pipeline
        self.current_step = 0
        self.anim_frame = 0
        self.max_anim_frames = 12
        self.scaled_data = None
        self.anchor_points = None
        self.shift_trajectories = []

        self._init_ui()

    def _generate_flight_data(self):
        np.random.seed(42)
        # אשכול 1: מרכז (דלתא נמוכה)
        c1 = np.array([32.10, 34.80, 500, 25.0, 1013, 5.0, 60, 1.2]) + np.random.randn(25, 8) * [0.03, 0.03, 15, 0.5,
                                                                                                 1.0, 0.5, 2.0, 0.15]
        # אשכול 2: דרום (דלתא גבוהה)
        c2 = np.array([31.50, 34.70, 1200, 16.0, 890, 14.0, 40, 4.5]) + np.random.randn(25, 8) * [0.03, 0.03, 20, 0.6,
                                                                                                  1.2, 0.8, 2.5, 0.2]
        # אשכול 3: צפון (דלתא בינונית)
        c3 = np.array([32.85, 35.20, 200, 29.0, 1020, 2.0, 85, 2.8]) + np.random.randn(25, 8) * [0.04, 0.04, 10, 0.4,
                                                                                                 0.8, 0.3, 1.5, 0.18]
        # נקודות רעש (Outliers)
        noise = np.array([
            [33.30, 35.60, 800, 12.0, 950, 22.0, 25, 8.5],
            [31.80, 35.10, 650, 22.0, 990, 8.0, 50, 3.1]
        ])
        return np.vstack([c1, c2, c3, noise])

    def _init_ui(self):
        main_widget = QWidget()
        self.setCentralWidget(main_widget)
        main_layout = QHBoxLayout(main_widget)

        # פאנל שליטה שמאלי
        control_panel = QFrame()
        control_panel.setFixedWidth(340)
        control_panel.setStyleSheet("background-color: #1e1e2f; color: #ffffff; border-radius: 8px; padding: 10px;")
        cp_layout = QVBoxLayout(control_panel)

        title_lbl = QLabel("<b>VORTEX AI Pipeline</b>")
        title_lbl.setStyleSheet("font-size: 16px; color: #4da6ff;")
        cp_layout.addWidget(title_lbl)

        self.status_lbl = QLabel("מצב: נתונים גולמיים נטענו")
        self.status_lbl.setStyleSheet("font-size: 13px; color: #a6b0cf;")
        cp_layout.addWidget(self.status_lbl)

        self.btn_next = QPushButton("התחל תהליך (Next Step)")
        self.btn_next.setStyleSheet(
            "background-color: #3b82f6; color: white; padding: 10px; font-weight: bold; border-radius: 5px;")
        self.btn_next.clicked.connect(self._next_step)
        cp_layout.addWidget(self.btn_next)

        self.btn_reset = QPushButton("איפוס מלא")
        self.btn_reset.setStyleSheet("background-color: #374151; color: white; padding: 8px; border-radius: 5px;")
        self.btn_reset.clicked.connect(self._reset)
        cp_layout.addWidget(self.btn_reset)

        log_title = QLabel("יומן ביצוע (Pipeline Logs):")
        log_title.setStyleSheet("margin-top: 15px; font-weight: bold;")
        cp_layout.addWidget(log_title)

        self.log_box = QTextEdit()
        self.log_box.setReadOnly(True)
        self.log_box.setStyleSheet(
            "background-color: #111827; color: #10b981; font-family: monospace; font-size: 11px;")
        cp_layout.addWidget(self.log_box)

        main_layout.addWidget(control_panel)

        # קנבס Matplotlib מרכזי
        self.fig = Figure(facecolor="#0f172a")
        self.canvas = FigureCanvas(self.fig)
        self.ax = self.fig.add_subplot(111)
        main_layout.addWidget(self.canvas)

        # טיימר לאנימציה
        self.anim_timer = QTimer()
        self.anim_timer.timeout.connect(self._animate_mean_shift_frame)

        self._plot_raw_data()

    def _log(self, text: str):
        self.log_box.append(f"> {text}")

    def _plot_raw_data(self):
        self.ax.clear()
        self.ax.set_facecolor("#1e293b")
        self.ax.grid(True, linestyle="--", alpha=0.3, color="#94a3b8")

        # ציר X: Longitude, ציר Y: Latitude, צבע: Delta
        sc = self.ax.scatter(
            self.raw_data[:, 1], self.raw_data[:, 0],
            c=self.raw_data[:, 7], cmap="plasma", s=65, edgecolors="#ffffff", alpha=0.9
        )
        self.ax.set_title("1. Raw Flight Telemetry (MongoDB)", color="#f8fafc", fontsize=13, pad=10)
        self.ax.set_xlabel("Longitude (°E)", color="#94a3b8")
        self.ax.set_ylabel("Latitude (°N)", color="#94a3b8")
        self.ax.tick_params(colors="#94a3b8")

        # Colorbar
        if not hasattr(self, 'cbar') or self.cbar is None:
            self.cbar = self.fig.colorbar(sc, ax=self.ax)
            self.cbar.set_label("Delta Error (Performance Shift)", color="#94a3b8")
            self.cbar.ax.yaxis.set_tick_params(color="#94a3b8")
            self.cbar.ax.tick_params(labelsize=9, labelcolor="#94a3b8")

        self.canvas.draw()
        self._log(f"נטענו {len(self.raw_data)} דגימות טיסה בנות 8 ממדים.")

    def _next_step(self):
        if self.current_step == 0:
            # שלב 1: נורמליזציה
            self.scaled_data = self.clusterer.scaler.fit_transform(self.raw_data)
            self.current_step = 1
            self.status_lbl.setText("מצב: Z-score Normalization הושלמה")
            self.btn_next.setText("הרץ אנימציית Mean Shift")
            self._plot_scaled_data()

        elif self.current_step == 1:
            # שלב 2: הכנת מסלולי ההתכנסות של Mean Shift והפעלת אנימציה
            self.btn_next.setEnabled(False)
            self.status_lbl.setText("מצב: Mean Shift מחפש צפיפויות...")
            self._prepare_trajectories()
            self.anim_frame = 0
            self.anim_timer.start(80)

        elif self.current_step == 2:
            # שלב 3: חילוץ עוגנים (Inverse Transform)
            self.current_step = 3
            self.status_lbl.setText("מצב: נקודות עוגן סופיות חולצו")
            self.btn_next.setText("פרוס שגיאות רציפות (Kriging)")
            self._plot_anchor_points()

        elif self.current_step == 3:
            # שלב 4: פריסת יריעת Kriging
            self.current_step = 4
            self.status_lbl.setText("מצב: משטח Kriging הושלם לגריד הניווט")
            self.btn_next.setText("התהליך הושלם (Restart)")
            self._plot_kriging_surface()

        elif self.current_step == 4:
            self._reset()

    def _plot_scaled_data(self):
        self.ax.clear()
        self.ax.set_facecolor("#1e293b")
        self.ax.grid(True, linestyle="--", alpha=0.3, color="#94a3b8")

        self.ax.scatter(
            self.scaled_data[:, 1], self.scaled_data[:, 0],
            c=self.scaled_data[:, 7], cmap="coolwarm", s=65, edgecolors="#ffffff", alpha=0.9
        )
        self.ax.axhline(0, color="#64748b", linestyle=":", linewidth=1)
        self.ax.axvline(0, color="#64748b", linestyle=":", linewidth=1)
        self.ax.set_title("2. Z-Score Space (μ=0, σ=1 Across All 8 Features)", color="#f8fafc", fontsize=13, pad=10)
        self.ax.set_xlabel("Scaled Longitude (Z-Score)", color="#94a3b8")
        self.ax.set_ylabel("Scaled Latitude (Z-Score)", color="#94a3b8")
        self.ax.tick_params(colors="#94a3b8")
        self.canvas.draw()
        self._log("נורמליזציה בוצעה: כל 8 הממדים אוזנו ליחידות מידה שוות.")

    def _prepare_trajectories(self):
        # הרצת האלגוריתם המלא
        self.clusterer.model.fit(self.scaled_data)
        scaled_centers = self.clusterer.model.cluster_centers_
        self.anchor_points = self.clusterer.scaler.inverse_transform(scaled_centers)

        # ייצור מסלול אינטרפולטיבי שמדגים את טיפוס הנקודות לפסגה
        labels = self.clusterer.model.labels_
        self.shift_trajectories = []
        for i, pt in enumerate(self.scaled_data):
            target = scaled_centers[labels[i]]
            # קו מתקדם מנקודת המוצא ליעד ב-max_anim_frames צעדים
            traj = np.linspace(pt, target, self.max_anim_frames)
            self.shift_trajectories.append(traj)

    def _animate_mean_shift_frame(self):
        self.ax.clear()
        self.ax.set_facecolor("#1e293b")
        self.ax.grid(True, linestyle="--", alpha=0.3, color="#94a3b8")

        curr_pts = np.array([traj[self.anim_frame] for traj in self.shift_trajectories])

        # ציור מסלולי התנועה
        for traj in self.shift_trajectories:
            self.ax.plot(traj[:self.anim_frame + 1, 1], traj[:self.anim_frame + 1, 0], color="#38bdf8", alpha=0.25,
                         linewidth=1)

        # ציור הנקודות הנוכחיות
        self.ax.scatter(curr_pts[:, 1], curr_pts[:, 0], color="#f43f5e", s=40, alpha=0.8)

        # ציור רדיוס ה-Bandwidth סביב המרכזים המתגבשים
        bw = self.clusterer.config.bandwidth
        scaled_centers = self.clusterer.model.cluster_centers_
        for c in scaled_centers:
            circle = FigureCanvas(None)  # רק כרפרנס
            circle_patch = self.ax.add_patch(
                FigureCanvas(None) if False else
                __import__("matplotlib.patches").patches.Circle((c[1], c[0]), bw, color="#38bdf8", fill=False,
                                                                linestyle="--", alpha=0.4)
            )

        self.ax.set_title(f"3. Mean Shift Convergence: Iteration {self.anim_frame + 1}/{self.max_anim_frames}",
                          color="#f8fafc", fontsize=13, pad=10)
        self.ax.set_xlabel("Scaled Longitude", color="#94a3b8")
        self.ax.set_ylabel("Scaled Latitude", color="#94a3b8")
        self.ax.tick_params(colors="#94a3b8")
        self.canvas.draw()

        self.anim_frame += 1
        if self.anim_frame >= self.max_anim_frames:
            self.anim_timer.stop()
            self.current_step = 2
            self.btn_next.setEnabled(True)
            self.btn_next.setText("בצע Inverse Transform לעוגנים")
            self._log(f"התכנסות הושלמה! אותרו {len(scaled_centers)} פסגות צפיפות.")

    def _plot_anchor_points(self):
        self.ax.clear()
        self.ax.set_facecolor("#1e293b")
        self.ax.grid(True, linestyle="--", alpha=0.3, color="#94a3b8")

        # רקע: הנתונים המקוריים שקופים
        self.ax.scatter(self.raw_data[:, 1], self.raw_data[:, 0], color="#64748b", s=30, alpha=0.3, label="Raw Flights")

        # הדגשת עוגני ה-Anchor Points שהוחזרו לסקאלה המקורית
        sc = self.ax.scatter(
            self.anchor_points[:, 1], self.anchor_points[:, 0],
            c=self.anchor_points[:, 7], cmap="plasma", s=220, edgecolors="#ffffff", linewidth=2, label="Anchor Centers"
        )

        for i, ap in enumerate(self.anchor_points, 1):
            self.ax.annotate(
                f"Anchor #{i}\nΔ={ap[7]:.2f}\nAlt={ap[2]:.0f}m",
                (ap[1], ap[0]), textcoords="offset points", xytext=(12, 10),
                color="#f8fafc", fontsize=9, fontweight="bold",
                bbox=dict(boxstyle="round,pad=0.3", fc="#1e1e2f", ec="#38bdf8", alpha=0.85)
            )

        self.ax.set_title("4. Extracted Anchor Points (Physical World Coordinates)", color="#f8fafc", fontsize=13,
                          pad=10)
        self.ax.set_xlabel("Longitude (°E)", color="#94a3b8")
        self.ax.set_ylabel("Latitude (°N)", color="#94a3b8")
        self.ax.tick_params(colors="#94a3b8")
        self.ax.legend(facecolor="#1e293b", edgecolor="#475569", labelcolor="#f8fafc")
        self.canvas.draw()

        self._log(f"חולצו {len(self.anchor_points)} עוגנים. נתוני ה-Delta סוכמו במדויק לכל אזור.")

    def _plot_kriging_surface(self):
        self.ax.clear()
        self.ax.set_facecolor("#1e293b")

        # יצירת גריד ניווט רציף על פני שטח הפעילות
        lon_min, lon_max = self.raw_data[:, 1].min() - 0.05, self.raw_data[:, 1].max() + 0.05
        lat_min, lat_max = self.raw_data[:, 0].min() - 0.05, self.raw_data[:, 0].max() + 0.05
        grid_lon, grid_lat = np.meshgrid(np.linspace(lon_min, lon_max, 100), np.linspace(lat_min, lat_max, 100))

        # אינטרפולציית RBF (שמדמה מתמטית את יריעת ה-Kriging מבוססת מרחקי Variogram)
        rbf = Rbf(self.anchor_points[:, 1], self.anchor_points[:, 0], self.anchor_points[:, 7], function='multiquadric',
                  smooth=0.1)
        grid_delta = rbf(grid_lon, grid_lat)

        # ציור מפת החום הרציפה
        contour = self.ax.contourf(grid_lon, grid_lat, grid_delta, levels=50, cmap="viridis", alpha=0.85)
        self.ax.contour(grid_lon, grid_lat, grid_delta, levels=10, colors="#ffffff", alpha=0.2, linewidths=0.5)

        # סימון עוגני ה-Mean Shift על גבי המשטח
        self.ax.scatter(self.anchor_points[:, 1], self.anchor_points[:, 0], c="#f43f5e", s=120, edgecolors="#ffffff",
                        linewidth=1.5, zorder=5)

        self.ax.set_title("5. Continuous Delta Error Surface (Kriging Output for A* Pathfinding)", color="#f8fafc",
                          fontsize=13, pad=10)
        self.ax.set_xlabel("Longitude (°E)", color="#94a3b8")
        self.ax.set_ylabel("Latitude (°N)", color="#94a3b8")
        self.ax.tick_params(colors="#94a3b8")
        self.canvas.draw()

        self._log("משטח השגיאות נפרס במלואו! מוכן להזנה לגריד הניווט של A*.")

    def _reset(self):
        self.current_step = 0
        self.btn_next.setEnabled(True)
        self.btn_next.setText("התחל תהליך (Next Step)")
        self.status_lbl.setText("מצב: נתונים גולמיים נטענו")
        self.log_box.clear()
        self._plot_raw_data()


if __name__ == "__main__":
    app = QApplication(sys.argv)
    window = FlightPipelineVisualizer()
    window.show()
    sys.exit(app.exec_())
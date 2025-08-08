## KexBuild - Grid-Based Building System

Version: 0.2 (simplified)
Purpose: Minimal, reusable grid-snapping building core for Unity ECS

### Core Philosophy

-   **Simplicity First**: Just grid snapping, no complex features
-   **0.5m Grid**: First placed object establishes grid origin
-   **No UI in Core**: Core only manages data and validation
-   **Minimal Dependencies**: Easy to drop into any Unity project

### Core Features

1. **Grid Snapping**

    - 0.5m increment grid
    - Grid origin established by first placement
    - All subsequent placements snap to this grid

2. **Rotation**

    - 15° increments (0, 15, 30, 45, 60, 75, 90, etc.)
    - Yaw rotation only

3. **Height Adjustment**

    - 0.5m increments
    - Vertical movement only

4. **Validation**

    - Simple AABB collision checking
    - Sets isValid flag on preview component

5. **Serialization**
    - Save/load placed objects
    - JSON format for easy debugging

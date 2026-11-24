# Graphic Primitives

This editable uGUI scene shows procedural shapes from the Tools package. Compare the examples in the Scene view or use their controls in Play Mode.

## Import and open

1. Open **Window → Package Management → Package Manager** and select **Tools**.
2. In **Samples**, import **Graphic Primitives**.
3. Open [`GraphicPrimitivesDemo.unity`](GraphicPrimitivesDemo.unity) under `Assets/Samples/Tools/<version>/Graphic Primitives`.

## Try it in Play Mode

| Example | What to try |
| --- | --- |
| Ellipse | Drag **Sweep** to distribute slices across an arc. Enable **Fixed slots** to reveal slices of a fixed size. Negative **Visible Segments** reveal counterclockwise slices. Fractional values reveal part of the final slice. |
| Rounded Rect | Drag **Corner Radius** and follow the dashed outline around the corners. |
| Arrow | Drag **Stroke Width** to change the width of the curve. |
| Sunburst | Toggle **Smooth profile** to compare the two profiles alongside the fixed sharp preview. |
| Non-Drawing | Click inside the outlined target and watch the hit count update. |

Use **Reset** to restore the authored settings and clear the hit count. Triangle, Closed Spline, Curved Rect, and Curved Inside Rect provide additional shapes to inspect in the scene. The Curved Rect example also shows clipping with a uGUI stencil mask.

## Edit the examples

- Select a shape in the Hierarchy to edit its geometry, fill, stroke, or rendering settings in the Inspector. Try **Segmented Fill** on an ellipse to adjust its slice count, inner radius, and gap independently.
- **Arrow Graphic** and **Closed Spline Graphic** share the **Edit Path** controls. Select a point to edit its Bézier handles. Choose **Broken**, **Mirrored**, or **Continuous**. **Insert Point** splits the next span without changing its shape.
- The spline closes from the last point to the first. Use **Mode** to choose fill, outline, or both. The path should form a simple contour.
- To texture an image-based shape, assign a sprite under **Rendering**. Use rectangular Sprite Atlas packing with **Allow Rotation** and **Tight Packing** disabled for these generated meshes. [Unity Sprite Atlas settings](https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/sprite/atlas/sprite-atlas-reference)

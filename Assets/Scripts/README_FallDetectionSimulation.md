# Fall Detection Synthetic Simulation Setup

This project is structured as a lightweight Unity simulation for synthetic fall-detection data, not as a game scene.

## Build the Home Scene

1. Open `Assets/Scenes/SampleScene.unity`.
2. Wait for Unity to finish compiling scripts. If the menu does not appear, choose `Assets > Refresh` and check the Console for compile errors.
3. In the Unity menu, choose `Fall Detection > Build Elderly Home Scene`.
4. The builder creates a root object named `Synthetic Elderly Home` and saves the current scene.
5. The builder creates and builds a `NavMeshSurface` automatically before enabling the elderly subject.

If Unity shows `Failed to create agent because there is no valid NavMesh`, rebuild the scene from `Fall Detection > Build Elderly Home Scene`. If the error remains, select `Synthetic Elderly Home`, find its `NavMeshSurface`, and press `Bake`. The surface is configured to collect all scene objects so it can use either the generated floor or an existing room floor.

If you cannot see the furniture or elderly subject, the builder has not been run yet. The C# files alone do not automatically place objects into the scene.

The generated environment contains:

- Furniture Mega Pack bed, bedside drawer, sofa, chair, dining table, and coffee table when the package is present.
- Realistic low-detail obstacles: slippers, rug, walking cane, box, and laundry basket.
- An elderly humanoid subject using `Assets/assets/Old Man Idle.fbx` if available.
- A fixed surveillance-style camera.
- Daily activity waypoints.
- A `Fall Scenario Manager` for later synthetic scenario generation and CSV pose/activity logs.

## Animator Setup

The scene builder creates `Assets/Scripts/ElderlySimulation.controller` with these parameters:

- `Speed` float
- `IsSitting` bool
- `Fall` trigger
- `Activity` int

The controller states are:

- `Idle Standing`
- `Slow Walk`
- `Sitting`
- `Falling`

The project imports Human Basic Motions clips from `Assets/Kevin Iglesias/Human Animations`. The builder assigns male idle, forward walk, and fall clips automatically when it creates or updates `ElderlySimulation.controller`.

Human Basic Motions FREE does not appear to include sit-down, seated-idle, or stand-up clips in this import. The generated controller currently reuses idle for `Sitting`, while the `ElderlyActivityAgent` still labels sitting and standing-up intervals for data collection. For more realistic video, add a dedicated sit/stand animation pack later and assign those clips to separate Animator states.

## Main Scripts

- `ElderlyActivityAgent.cs`: automatic daily activity loop for standing, slow walking, sitting, standing up, and moving around the room.
- `FallScenarioManager.cs`: starts fall scenarios and logs subject pose/activity to CSV in `Application.persistentDataPath`.
- `SurveillanceCameraRig.cs`: keeps a fixed high-corner camera angle similar to a surveillance camera.
- `ElderlyHomeSceneBuilder.cs`: editor menu tool that creates the optimized home simulation scaffold.

## Data Generation Direction

For many synthetic scenarios, vary:

- Random seed.
- Starting subject position.
- Walking route.
- Fall delay.
- Fall type.
- Obstacle position.
- Camera FOV and placement.
- Clothing/material colors if needed for domain randomization.

Keep geometry simple and colliders clean. For fall detection datasets, consistency, labels, camera placement, and scenario diversity are more important than high-end visual detail.

## Realistic Elderly Apartment Layout

The builder now instantiates the main Furniture Mega Pack prefabs directly. The remaining generated cube/cylinder objects are intentional low-detail simulation props for fall-risk context and labeling.

Recommended hierarchy:

- `Environment`: room shell, floor, walls, baseboards, windows, curtains, lighting.
- `Furniture`: bed area, living area, table area, storage, risk objects.
- `Character`: Human Basic Motions humanoid, Animator, NavMeshAgent, activity controller.
- `Camera`: fixed surveillance-style capture camera.
- `Data Collection`: scenario manager, labels, logging, capture scripts, metadata.

Recommended room zoning for the current 8 m x 6 m layout:

- Bed area: keep the bed along the back-left wall near `(-2.35, 0, 1.65)`. Leave at least 0.8 m clearance along one side so the character can stand up, walk, and fall beside the bed.
- Chair near bed: place a stable chair near `(-1.75, 0, -1.45)` or slightly closer to the bed if you want bed-to-chair transfers. Keep `Chair Sit Target` at realistic seat height, around 0.45-0.55 m above floor depending on the model.
- Sofa/living area: place the sofa along the back-right wall around `(2.45, 0, 1.55)`, facing the coffee table. Leave a clear path between sofa, table, and room center.
- Table area: place dining or side table around `(-0.55, 0, -1.45)`. Avoid blocking the path from door to bed.
- Walking paths: preserve a route from front doorway to bed, bed to chair, chair to table, table to sofa, and sofa back to center. Target 0.8-1.2 m corridor width.
- Fall-risk areas: use a low rug at room center, slippers near the bed path, a cane near the wall, and a laundry basket or small box near the sofa edge. These should be visually realistic but not so dense that falls become physically impossible.

## Furniture Mega Pack Replacement Map

The builder currently uses these prefab paths:

- `Assets/Furniture Mega Pack/Prefabs/Beds/Bed01.prefab`
- `Assets/Furniture Mega Pack/Prefabs/Drawers/Drawer01.prefab`
- `Assets/Furniture Mega Pack/Prefabs/Chairs/Chair01.prefab`
- `Assets/Furniture Mega Pack/Prefabs/Sofas/Sofa01.prefab`
- `Assets/Furniture Mega Pack/Prefabs/Tables/Table01.prefab`
- `Assets/Furniture Mega Pack/Prefabs/Tables/Table06.prefab`

If you prefer different models, change the path constants at the top of `Assets/Editor/ElderlyHomeSceneBuilder.cs`.

Manual replacement reference:

| Current object | Replacement target | Notes |
| --- | --- | --- |
| `Bed Frame`, `Mattress`, `Pillow` | Complete bed prefab or bed + mattress prefabs | Align headboard with the back wall. Keep bed height realistic, roughly 0.45-0.65 m. |
| `Bedside Table` | Nightstand prefab | Keep it reachable from bed but outside the main walking corridor. |
| `Lamp` | Table lamp prefab | Use a low-intensity point or spot light only if it helps realism. |
| `Chair Seat`, `Chair Back` | Armchair or dining chair prefab | Keep `Chair Sit Target`; align its rotation to the seated pose. |
| `Sofa Base`, `Sofa Back`, arms, cushions | Sofa prefab | Prefer one low-poly sofa prefab rather than many separate high-poly cushions. |
| `Coffee Table` | Coffee table prefab | Keep enough clearance so the humanoid can pass and fall near it. |
| `Dining Table` | Small table prefab | Avoid sharp scale changes; unrealistic scale hurts data quality. |
| `Rug Low Contrast` | Thin rug prefab or plane | Collider should be very thin or disabled if it interferes with walking. |
| `Slippers Trip Object` | Slippers/shoes prefab | Good fall-risk object; keep low and near a walking path. |
| `Small Box Obstacle`, `Laundry Basket` | Basket, box, clutter prefab | Use sparingly; the scene should look lived-in, not blocked. |
| `Walking Cane`, `Walker*` | Cane/walker prefabs | Place near bed/chair as elderly-home context cues. |
| `Wall Picture`, `Curtain*`, `Plant*` | Decor prefabs | Good for realism; mark static and avoid unnecessary colliders. |

For each replacement prefab:

1. Drag the prefab into the same parent under `Furniture`.
2. Match the placeholder position, rotation, and approximate footprint.
3. Disable or delete the placeholder renderer after confirming the prefab fits.
4. Keep simple colliders: use box/capsule colliders for furniture and avoid mesh colliders unless necessary.
5. Mark non-moving furniture as Static for batching and NavMesh baking.
6. Re-bake the NavMesh and verify the `Elderly Subject` can reach every waypoint.

## Human Basic Motions Setup

Use the Human Basic Motions clips as humanoid retargeted clips on the elderly character.

Animator state mapping:

| Animator state | Clip type |
| --- | --- |
| `Idle Standing` | idle standing or idle breathing |
| `Slow Walk` | slow walk, elderly walk, or reduced-speed walk cycle |
| `Sitting` | seated idle |
| `StandingUp` activity | sit-to-stand clip if you add a dedicated Animator state |
| `Falling` | forward fall, backward fall, side fall, or collapse animation |

Recommended improvements:

- Add separate `Sit Down` and `Stand Up` Animator states when you have clips for them.
- Use root motion only if the clip movement is stable and matches the NavMeshAgent; otherwise keep movement controlled by `ElderlyActivityAgent`.
- Keep walking speed slow, around 0.6-0.9 m/s, for elderly daily activity.
- Add several fall variants later: trip near rug, fall while standing up, collapse while walking, fall beside bed, fall near table.

## Fixed Monitoring Camera

The current `SurveillanceCameraRig` places the camera near `(3.65, 2.75, -2.85)` with a field of view around 72 degrees.

Use this as a home-monitoring camera:

- Mount high in a room corner, around 2.4-2.9 m.
- Angle downward to see the bed, chair, sofa, table, and center walking path.
- Avoid extreme wide angle if pose extraction matters; 60-75 degrees is a useful range.
- Keep the camera fixed for baseline experiments.
- Later, generate secondary datasets with 2-3 alternate fixed camera positions for domain diversity.

## Unity Settings for Realistic Synthetic Data

Lighting:

- Use URP if available.
- Use one soft ceiling or area light plus ambient light.
- Avoid dramatic game lighting; fall detection data should resemble ordinary indoor monitoring footage.
- Bake static lighting if the scene is static and you need performance.

Materials:

- Use realistic but simple materials: wood, fabric, painted wall, matte floor.
- Avoid overly reflective floors or strong procedural noise that may confuse later vision models.
- Add mild material variation only after the baseline dataset works.

Physics and navigation:

- Use simple colliders on all fall-relevant objects.
- Disable colliders on purely decorative small items unless they are intentional obstacles.
- Keep rug colliders thin or trigger-only.
- Bake NavMesh after every major layout change.
- Leave enough open floor for natural fall animations; target at least one 1.5 m x 1.5 m clear fall area.

Optimization:

- Prefer low/medium-poly furniture prefabs.
- Mark static environment and furniture objects as Static.
- Use LODs only if the imported pack already provides them.
- Keep texture resolution moderate, for example 1K or 2K for furniture.
- Remove unused demo scenes, cameras, lights, and scripts from imported packs.

Data quality:

- Keep object names semantically meaningful for labels and metadata.
- Record camera transform, scenario type, fall time, subject pose, and activity label with every capture.
- Build a clean baseline scene first, then create randomized variants.
- Separate realism from randomness: first validate one realistic apartment, then vary furniture positions, lighting, clothing, and fall location in controlled ranges.

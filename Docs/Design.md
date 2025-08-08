## BuildCore — Reusable Building System (Engine‑Agnostic, UI‑Agnostic)

Version: 0.1 (design)
Owner: Dylan
Purpose: Headless, reusable building core for first/third‑person simulation games. The game’s UI layer references BuildCore one‑way.

### Goals

-   UI‑agnostic, renderer‑agnostic, engine‑agnostic core
-   Deterministic, data‑driven, mod‑friendly
-   Minimal assumptions: flat or arbitrary surfaces, upright or free tilt, yaw snaps
-   Snapping that feels “smart” without menus
-   Supports presets/blueprints, chain placement, socketed modules (queue painting deferred to stretch)

### Conceptual Walkthrough (read first)

What BuildCore is

-   A smart "placement brain". Your game asks for suggestions; BuildCore returns tidy snaps and simple yes/no checks.
-   It does not render, read inputs, or simulate physics. Your game does those via an adapter.

Design philosophy (why it’s shaped this way)

-   Minimal UI: first‑person, tactile building should feel neat without menus.
-   Predictable snaps: always try sockets first, then local grids, then edge‑to‑edge, then free.
-   Local, not global grids: the first anchor in an area defines a grid that fits that item family.
-   Semantic correctness: some things must attach to the right place (e.g., ride panels). That’s what sockets/links are for.
-   Deterministic, data‑driven: same world → same suggestions; content lives in definitions, not code.

Your mental model (5 steps)

1. Catalog: a list of placeable things with basic facts (size, rotation rules, sockets, clearances).
2. Suggest: point somewhere → BuildCore offers good placements (why: socket, grid, edge, free).
3. Decide: cycle suggestions or detach snapping for freedom.
4. Commit: accept to place it; undo/redo works.
5. Link: if the item belongs to another (panel → ride), the link is recorded.

Simple flows (examples)

-   Bench in a row: first bench creates a local grid; the next benches snap to that grid or edge‑align into a perfect row.
-   Fence run: place first post, then click along a path; posts auto‑space and corner at 90°/45° until you stop.
-   Ride operator panel: approach a ride; the panel snaps to a compatible panel_mount socket; commit to both place and link.
-   Queue strips: place preset strips or a switchback blueprint; final strip snaps to a station gate socket.

Why ECS (high level)

-   Complexity becomes small components (facts) and simple systems (rules) running on streams of entities.
-   Stable things (placed objects) are entities with tiny components; transient things (intents/suggestions) appear, get processed, and disappear.
-   Deterministic pipelines keep outputs repeatable and debugging sane.

Relating to familiar games

-   Supermarket Simulator: shelves/fridges align cleanly via local grids and edge snaps; Shift to free‑place when you want gaps.
-   TCG Card Shop: counters/tables form tidy rows on a small grid; optional blueprints drop in ready‑made bays.
-   Valheim: cycle snap points when several are valid; sockets beat grids; detach snapping when needed.
-   RuneScape Dragonwilds: the first placed piece establishes a build axis/grid that later pieces follow.

Quick start (host integration)

-   Provide raycasts/overlaps/support planes + time/IDs via an adapter.
-   Start preview(defId) → update preview(cursor) to get suggestions → show ghost.
-   Let the player rotate, detach, or cycle; on accept, commit. Listen for events.

### Two‑Tier Plan: Box‑Only In‑Game + Flexible UGC Core

-   Tier A — In‑Game (No UI, Box‑Only)

    -   Player never sees an inventory. Content arrives as physical boxes.
    -   Unbox → preview the item/kit → place with yaw rotation → hold to move → hold E to re‑box.
    -   Variants chosen via physical affordances: printed “A/B/C” toggle on the box, a flip switch on the kit, or a paper blueprint card placed before unboxing.
    -   Preset‑first: rides as kits; queue switchback kits; concession bay kits; decor rows; fence reels; cable reels; path tiles/strips; station kits.
    -   Links are diegetic: hold E to pair booth↔entrance, panel↔ride. LEDs confirm.
    -   No paint editors or menus. Complex assemblies are delivered as multiple labeled boxes with a simple assembly order card.

-   Tier B — Core/UGC (Authoring & Modding)
    -   Full BuildCore grammar: sockets, local grids, adjacency, blueprints, chain; path painting is an optional module.
    -   Authors create presets/kits/blueprints offline. The game ships only the curated Tier‑A presets.
    -   UGC content remains possible without exposing heavy in‑game tools.

### Architecture

-   Core Runtime (pure domain logic)
    -   CatalogService: registers BuildableDefinitions
    -   PlacementEngine: resolves poses from intents + world queries
-   SnapResolver: sockets, local grids, adjacency/butt, chain
    -   ValidationEngine: collisions, clearances, dependencies, rules
    -   LinkGraph: entity↔entity required/optional links
    -   BlueprintEngine: load/parameterize/instantiate blueprints
    -   Session: transactions, undo/redo, ephemeral previews, grouping
    -   Persistence: scene graph I/O, versioned schema
    -   EventBus: domain events with payloads
-   Host Integration (GameAdapter)
    -   World queries supplied by host (raycasts, overlaps, support planes)
    -   Time/IDs, asset lookups, permission gates
    -   Core emits events; host renders ghosts/SFX/VFX and applies game state
-   UI Layer (Game Specific)
    -   Translates input to intents; cycles snaps; shows previews; calls commit/cancel

### ECS-Oriented Breakdown (engine-agnostic, Unity DOTS friendly)

-   Data orientation

    -   Prefer immutable catalogs as blob assets (or equivalent) loaded once.
    -   Express world state as entities with small, focused components and dynamic buffers.
    -   Encode transient actions (intents, offers) as entities processed and culled per frame.

-   Core components (examples)

    -   BuildableDefinitionRef: pointer/hash to catalog entry
    -   Footprint: bounds, height, clearance
    -   RotationPolicy: yawSnapDegrees, allowTilt, upVector
    -   LocalGridAnchor: cellSize
    -   Socket: tag, localPose (use DynamicBuffer<Socket>)
    -   ChainSpec: stride, allowedAnglesMask, autoCorner
    -   ValidationRules: bitmask/params for collisions, clearances, zones
    -   Dependencies: requiredParentTags, optionalLinkTags, maxLinkDistance
    -   Link: targetEntity, tag (use DynamicBuffer<Link> for multi-links)
    -   Preview: currentPose, selectedOfferIndex, mode(place|chain|paint), detachSnap
    -   Offer: pose, reason (socket|grid|adjacency|free), score, conflictMask (DynamicBuffer<Offer>)
    -   Intent: defId, cursorPose, options (entity created by UI; consumed by systems)
    -   Blueprint: items (buffer of relative transforms + tags)
    -   SceneTag/OwnedByScene: grouping and persistence boundaries
    -   UndoMarker: snapshot/transaction grouping id

-   Systems (high level)

    -   CatalogBakingSystem: bake BuildableDefinitions into blob assets/lookup tables
    -   IntentCaptureBridge: converts game/UI input into Intent entities
    -   PlacementSuggestionSystem: for each Intent → generate Offer buffer on a Preview entity
    -   SnapResolutionSystem: order/filter offers (sockets > grid > adjacency > free)
    -   ValidationSystem: evaluate collisions/clearances/dependencies; annotate offers/conflicts
    -   PreviewUpdateSystem: choose best valid offer; update Preview.pose for ghost rendering
    -   ChainPlacementSystem: manage start/continue/end of chain mode; emit multiple placements
    -   PaintPlacementSystem: build path/queue segments; ensure connectivity to sockets
    -   LinkResolutionSystem: create/update Link buffers; enforce required links
    -   CommitSystem: on commit request, instantiate placed entities, assign components, record UndoMarker
    -   UndoRedoSystem: apply snapshot diffs to revert/restore
    -   BlueprintInstantiateSystem: resolve anchors, place item set, create links
    -   PersistenceSystem: export/import scene entities/components
    -   EventDispatchSystem: translate domain changes into host/game events (if needed)

-   Data flow (per placement)

    1. UI creates Intent entity with defId + cursorPose + options
    2. PlacementSuggestionSystem queries nearby anchors/sockets/world via adapter, fills Offer buffer
    3. SnapResolutionSystem sorts/cycles offers; ValidationSystem marks conflicts
    4. PreviewUpdateSystem updates Preview.pose; UI renders ghost
    5. On commit, CommitSystem converts preview to placed entity, sets BuildableDefinitionRef, Footprint, etc., updates Link
    6. Intent and transient Preview/Offers are destroyed or reused

-   Host adapter in ECS

    -   Provide query systems/components for raycasts/overlaps/support planes (e.g., PhysicsWorldSingleton wrappers)
    -   Provide unique id/time singletons; register external obstacles as entities with CollisionProxy
    -   Consume Event components or listen to EventDispatchSystem outputs

-   Notes
    -   Keep components small and cohesive; prefer dynamic buffers for lists (sockets, offers, links, blueprint items)
    -   Use Aspects to group common reads (e.g., BuildableAspect = DefinitionRef + Footprint + RotationPolicy)
    -   Keep systems stateless; prefer deterministic iteration order (e.g., ISystem with explicit sorting)

### Data Model (BuildableDefinition)

-   id, category, tags
-   footprint: bounds, pivot, height, clearance
-   rotationPolicy: yawSnapDegrees, allowTilt, upVector
-   localGrid: {isAnchor, cellSize}
-   sockets: [{id, tag, pose, rules}]
-   chainSpec: {stride, allowedAngles:[90,45], autoCorner:true}
-   validation: collisions, requiredClearances, accessAisles, permitZones
-   dependencies: requiredParentTags, optionalLinkTags, maxLinkDistance
-   group: kitId, requiredModules, optionalModules

### Core Concepts

-   Intent: desired placement for buildable X at target cursor pose
-   Offer: list of resolved snap candidates with scores and reasons
-   Decision: selected offer → preview pose and validation state
-   Commit: materialize entity with stable id; emit Created + LinksUpdated
-   Link: semantic relation (e.g., panel→ride), enforced by rules
-   Blueprint: parametrized set of placements with relative transforms and tags

### Snapping Priorities (trimmed MVP)

1. Socket snap (by compatible tags)
2. Local grid snap (nearest anchor of same category)
3. Adjacency/butt snap (edge align to neighbor)
4. Free pose (constrained by rotation policy)

### APIs (conceptual)

-   Catalog
    -   registerDefinition(def), getDefinition(id), listByTag(tag)
-   Session
    -   begin(), end(), undo(), redo()
-   Placement
    -   startPreview(defId)
    -   updatePreview(cursorPose, options:{cycleIndex, detachSnap, mode:place|chain|paint}) → Offer[]
    -   selectOffer(index)
    -   commitPreview() → EntityId
    -   cancelPreview()
-   Chain
    -   startChain(defId, firstPose)
    -   continueChain(cursorPose) → Offer[]
    -   endChain()
-   Links
    -   link(aId, bId, tag), unlink(aId, bId)
    -   findLinkables(sourceId, tag, radius)
-   Persistence
    -   exportScene(), importScene()
-   Events (subscribe)
    -   PreviewUpdated, PlacementCommitted, PlacementRejected, LinksUpdated, SceneChanged

### Host/GameAdapter Contract

-   Must Provide
    -   raycast(surfaceMask), raycastAgainstCoreEntities, overlap(bounds)
    -   getSupportPlane(cursorPose) → plane (for upright constraint)
    -   getExistingWorldObstacles() (not managed by core)
    -   now(), newStableId()
-   Receives
    -   domain events with resolved world poses and metadata

### Validation Rules (trimmed MVP)

-   Collisions and interpenetration
-   Simple clearances (e.g., min aisle)
-   Dependency satisfaction (required parent tag within range)

### Blueprints

-   Definition: {id, title, items:[{defId, relTransform, requiredTags, params}], anchors:[tags]}
-   Instantiate: provide anchor(s) → absolute transforms resolved via SnapResolver
-   Shareable: references by defId/tag; soft‑fail when missing

### Determinism

-   Same input intent + same world query results → same Offer ordering and selection
-   Versioned schema for forward/backward compatibility

### Modding Surface

-   JSON/YAML BuildableDefinitions
-   Tag taxonomy owned by content pack
-   Localized titles/descriptions separate from logic

### MVP for BuildCore (downscoped)

-   CatalogService, PlacementEngine, SnapResolver (sockets/grid/adjacency)
-   ValidationEngine (collision, simple clearance, required‑link range)
-   LinkGraph (basic link/unlink)
-   Blueprint instantiate (fixed transforms)
-   Transactions (basic undo/redo)
-   Persistence (scene export/import)

### Stretch (later or UGC tier)

-   Path/queue paint with junctions and widths
-   Auto edge‑socket generation on painted paths
-   Chain 45° corners and spacing presets
-   Parametric buildables (length N segments, etc.)
-   Surface‑conforming (non‑flat support planes)
-   Offer scoring/variety beyond priority order
-   Rich permit/zone validation

### Out of Scope (Core)

-   Rendering, input, physics simulation, audio, AI

### Example UI Flow (First‑Person)

-   Unbox item (UI) → startPreview(defId)
-   Move cursor; core returns Offers with best snap; UI shows ghost
-   Q/E cycles offers; Shift detaches snap; R changes yaw snap preset
-   Click commitPreview()
-   Hold LMB to startChain; release to endChain
-   Hold E on entity to request link candidates; UI shows highlights; click to link

### Notes for Backyard Coaster Integration

-   Configure rotationPolicy to upright‑only, yaw snaps 90/45/free
-   Enable sockets for ride modules; local grids for concessions/decor
-   Use chain for fences/lights; paint for queues/paths
-   Reference: see `Docs/BackyardCoaster_GDD.md` (Building System section)

### Authoring Examples (definitions + placement experience)

#### 1) Simple Building (floors, walls, ceilings)

Goal: Author neat tiles and walls that align without a global grid. Walls can be inner, center, or outer relative to a floor tile’s edge. Upright-only, yaw snaps at 90°.

Cell sizes

-   Floors: 1.0 m tiles
-   Walls: 0.2 m thickness, 1.0 m span per segment
-   Ceilings: 1.0 m panels at a fixed height (e.g., +3.0 m)

Key idea: Floor tiles expose multiple wall-mount sockets along each edge at slightly different offsets: inner, center (straddling), outer. Wall segments prefer these sockets. If none found, they fall back to local-grid or adjacency.

Pseudo-definitions (YAML-like)

```
BuildableDefinition: FloorTile_1m
  category: Structure
  footprint: { size: [1.0, 1.0], height: 0.1, clearance: 0.0 }
  rotationPolicy: { yawSnapDegrees: [90], allowTilt: false }
  localGrid: { isAnchor: true, cellSize: 1.0 }
  sockets:
    # North edge wall mounts at three offsets
    - { id: n_in,  tag: wall_mount_n_inner,  pose: { pos:[0.0, +0.5, 0.0], rot:0 } }
    - { id: n_ctr, tag: wall_mount_n_center, pose: { pos:[0.0, +0.5, 0.0], rot:0 } }
    - { id: n_out, tag: wall_mount_n_outer,  pose: { pos:[0.0, +0.5, 0.0], rot:0 } }
    # Repeat for e/s/w edges with appropriate rotations

BuildableDefinition: Wall_1m
  category: Structure
  footprint: { size: [1.0, 0.2], height: 3.0, clearance: 0.0 }
  rotationPolicy: { yawSnapDegrees: [90], allowTilt: false }
  chainSpec: { stride: 1.0, allowedAngles: [90], autoCorner: true }
  sockets:
    - { id: a, tag: wall_end, pose:{ pos:[-0.5, 0.0, 0.0], rot:0 } }
    - { id: b, tag: wall_end, pose:{ pos:[+0.5, 0.0, 0.0], rot:0 } }
  validation:
    collisions: solid
    requiredClearances: []
    accessAisles: []
    permitZones: []
  dependencies:
    requiredParentTags: [ wall_mount_* ]  # prefers snapping to tile edge sockets

BuildableDefinition: Wall_DoorFrame_1m
  category: Structure
  footprint: { size: [1.0, 0.2], height: 3.0, clearance: 0.0 }
  rotationPolicy: { yawSnapDegrees: [90], allowTilt: false }
  sockets:
    - { id: a, tag: wall_end, pose:{ pos:[-0.5, 0.0, 0.0], rot:0 } }
    - { id: b, tag: wall_end, pose:{ pos:[+0.5, 0.0, 0.0], rot:0 } }
  validation:
    collisions: passthrough_center  # allows doorway opening
  dependencies:
    requiredParentTags: [ wall_mount_* ]

BuildableDefinition: CeilingTile_1m
  category: Structure
  footprint: { size: [1.0, 1.0], height: 0.05, clearance: 0.0 }
  rotationPolicy: { yawSnapDegrees: [90], allowTilt: false }
  localGrid: { isAnchor: true, cellSize: 1.0 }
  validation:
    # Must lie above a filled floor/room footprint; host may supply a rule or volume test
    permitZones: [ room_interior_only ]
```

Placement experience

-   Floors: place one `FloorTile_1m`; it creates a 1.0 m local grid. Continue placing to form the room footprint.
-   Walls: start `Wall_1m`. Near a tile edge, BuildCore proposes socket snaps:
    -   Press Q/E to choose inner vs center vs outer mount on that edge.
    -   Chain mode: click along the perimeter; corners auto at 90°; use `Wall_DoorFrame_1m` where needed.
-   Ceilings: once walls enclose a valid room, place `CeilingTile_1m`; they snap to the ceiling grid above the floor layout.

Notes

-   The “inner/center/outer” choice is exposed through distinct socket tags per tile edge; author content packs with the offsets that fit art thickness.
-   If you don’t want socket authorship on floors, you can rely on local grid + adjacency: walls butt‑snap their ends to each other and align their long edge to the nearest grid line.

Blueprint example: “3×3 room”

```
Blueprint: Room_3x3
  items:
    - { defId: FloorTile_1m, rel:[-1, -1, 0] } ... { nine tiles }
    - { defId: Wall_1m, rel:[-1.5, 0, 0], rot:90 } ... { perimeter run }
    - { defId: Wall_DoorFrame_1m, rel:[0, +1.5, 0], rot:0 }
    - { defId: CeilingTile_1m, rel:[-1, -1, +3] } ... { nine tiles }
  anchors: []
```

#### 2) Queue Path with Fences

Goal: Paint a queue path that connects to a station gate socket. Chain fence segments along the queue edges with neat turns.

Definitions

```
BuildableDefinition: QueuePath
  category: Path
  footprint: { size:[segment], height:0.0, clearance:0.0 }
  rotationPolicy: { yawSnapDegrees:[45,90], allowTilt:false }
  # Paint mode: core builds a polyline; an entity holds samples + derived sockets
  validation:
    requiredClearances: [ minWidth: 1.5 ]
    dependencies: { requiredParentTags: [ station_gate ] }  # path must end at a station gate socket

BuildableDefinition: FenceSegment_1m
  category: Fence
  footprint: { size:[1.0, 0.1], height:1.2, clearance:0.0 }
  rotationPolicy: { yawSnapDegrees:[45,90], allowTilt:false }
  chainSpec: { stride: 1.0, allowedAngles:[45,90], autoCorner:true }
  sockets:
    - { id: a, tag: fence_end, pose:{ pos:[-0.5,0,0], rot:0 } }
    - { id: b, tag: fence_end, pose:{ pos:[+0.5,0,0], rot:0 } }
  validation: { collisions: solid }
```

Station sockets (on the coaster/ride station entity)

```
Station
  sockets:
    - { id: q_in,  tag: station_gate, pose:{ pos:[x,y,0], rot:θ } }
    - { id: q_out, tag: station_exit, pose:{ pos:[x',y',0], rot:θ' } }
```

Placement experience

-   Paint queue: select `QueuePath`, click‑drag to draw switchbacks; the line neatens to the local grid; snap the last point to the station’s `station_gate` socket (it will be suggested first).
-   Place fences: start `FenceSegment_1m` in chain mode next to the path edge.
    -   Edge snapping: the queue path entity exposes generated sockets along both edges (e.g., `queue_edge_left`, `queue_edge_right`) at 1.0 m intervals. BuildCore prefers these when nearby, so segments align perfectly.
    -   Corners: 90°/45° turns auto‑corner; continue clicking to outline the queue.

Notes

-   If you don’t want the path to generate edge sockets, fence segments can still align via local grid + adjacency. It’s a bit less precise around tight corners.
-   You can also author a small “QueueSwitchback” blueprint: a repeating U‑pattern of FenceSegments at 1.0 m spacing that you stamp down, then paint the path through it.

# Isometric Game Editor

> Stack: .NET 10 · Raylib-cs · ImGui.NET · rlImGui-cs · System.Text.Json · xUnit v3
> Architecture: Clean — Editor / Rendering / Infrastructure → Application → Domain
> เฟส 1–8 = Vertical Slice · เฟส 9+ = หลัง slice ทำงานแล้ว
> สถานะ (2026-10-05): `dotnet test` 49/49 pass · ค้าง: 9.3+

## เฟส 1: Setup Solution

- [x] 1.1 สร้าง `GameEditor.slnx`
  - test: `dotnet sln GameEditor.slnx list` รันได้ไม่ error
- [x] 1.2 สร้างโปรเจกต์ Domain, Application, Infrastructure, Rendering (classlib), Editor (console), Tests (xUnit v3) แล้วเพิ่มเข้า sln
  - test: `dotnet sln list` แสดงครบ 6 โปรเจกต์
- [x] 1.3 `Directory.Build.props`: net10.0, Nullable, ImplicitUsings
  - test: `dotnet build` pass
- [x] 1.4 `Directory.Packages.props`: pin Raylib-cs, ImGui.NET, rlImGui-cs, xUnit v3 (rlImGui-cs ต้องตรงรุ่น Raylib-cs)
  - test: `dotnet restore` pass ไม่มี version conflict warning
- [x] 1.5 Project reference ตามชั้น (Application→Domain, Infrastructure→Application, Rendering→Domain, Editor→ทุกชั้น, Tests→Domain+Application+Infrastructure)
  - test: `dotnet build` pass + Domain.csproj ไม่มี PackageReference/ProjectReference
- [x] 1.6 `InternalsVisibleTo` ใน Domain ให้ Application, Tests
  - test: `dotnet build` pass + `dotnet test` รันได้ (0 test ก็ได้)

## เฟส 2: Domain

- [x] 2.1 `TileCoord` (record struct)
  - test: `dotnet build` pass
- [x] 2.2 `TileMap`: Width, Height, `int[]` tiles, `Get`, `InBounds`, `internal Set`
  - test: `dotnet test` — Get/Set/InBounds ถูก, ขอบแผนที่ถูก
- [x] 2.3 Ctor `TileMap` เช็ก `width * height == tiles.Length`
  - test: `dotnet test` — ขนาดไม่ตรง → throw
- [x] 2.4 `IsoMath`: `TileToScreen` / `ScreenToTile` (64×32)
  - test: `dotnet test` — round-trip ทุกช่องใน 10×10 ได้ค่าเดิม, จุดขอบข้าวหลามตัดได้ช่องถูก

## เฟส 3: Application

- [x] 3.1 `ICommand` (Execute/Undo) + `UndoStack` (Push/Undo/Redo/Clear, command ใหม่ล้าง redo)
  - test: `dotnet test` — undo/redo ลำดับถูก, push ใหม่แล้ว redo ว่าง
- [x] 3.2 `PaintTilesCommand` จำค่าเดิมทุกช่อง
  - test: `dotnet test` — execute แล้ว undo ได้แผนที่เหมือนเดิมเป๊ะ
- [x] 3.3 `MapEditingService`: BeginStroke / Paint / EndStroke, Undo, Redo
  - test: `dotnet test` — ลาก 1 ครั้ง (หลายช่อง) = undo 1 ครั้งย้อนหมด, tile ขึ้นทันทีระหว่าง Paint
- [x] 3.4 Port `IMapRepository` + Result + Save/Load ใน service (Load เรียก `UndoStack.Clear()`)
  - test: `dotnet test` ด้วย `FakeMapRepository` — Load แล้ว undo ว่าง, repo error → Result fail ไม่ throw

## เฟส 4: หน้าต่าง Editor

- [x] 4.1 เปิดหน้าต่าง raylib + main loop
  - test: `dotnet run --project src/GameEditor.Editor` เปิดหน้าต่าง ปิดได้ไม่ crash
- [x] 4.2 `rlImGui.Setup` เปิด docking + `DockSpaceOverViewport` (PassthruCentralNode)
  - test: รันแล้วลาก panel (ShowDemoWindow) ไป dock ได้, กลางจอโปร่ง
- [x] 4.3 Composition root ใน `Program.cs` (new ประกอบทุกชั้น)
  - test: `dotnet build` pass + รันได้
- [x] 4.4 MenuBar: File (Open/Save/Save As) + ชื่อ map ปัจจุบัน + `*` เมื่อยังไม่ save
  - test: รันแล้วเห็นเมนูครบ กดได้ไม่ crash
- [x] 4.5a `MapEditingService.New()`: map ว่างขนาดเดิม, `CurrentName = null`, undo ล้าง, `IsDirty = false`
  - test: `dotnet test` — วาด+save แล้ว New → tile ว่างหมด, CanUndo/CanRedo false, CurrentName null, ไม่ dirty ✓ `dotnet test` pass (45/45)
- [x] 4.5 File → New (ถามยืนยันถ้ามีแก้ค้าง) ผ่าน `ConfirmDiscard`
  - test: รัน — วาด → New → ขึ้นถาม → Discard ได้ map ว่าง, undo ว่าง ✓ user ยืนยัน pass
- [x] 4.6 เมนู Edit (Undo/Redo, disable เมื่อ `CanUndo`/`CanRedo` = false)
  - test: รัน — กดเมนู Undo/Redo ได้ผลเหมือน Ctrl+Z/Y ✓ user ยืนยัน pass

## เฟส 5: Rendering

- [x] 5.1 `MapRenderer` วาดตารางข้าวหลามตัด + สีตาม TileId, จัดกลางจอ (map 20×20)
  - test: รันแล้วเห็นตาราง iso 20×20 หลัง panel

## เฟส 6: Paint Tool

- [x] 6.1 เมาส์ → world → tile + highlight ช่องที่ชี้ (ข้ามถ้า `WantCaptureMouse`)
  - test: รัน — ขยับเมาส์ highlight ตรงช่อง, เมาส์บน panel ไม่ highlight ✓ user ยืนยัน pass
- [x] 6.2 คลิก/ลากวาด (กด = BeginStroke, ปล่อย = EndStroke)
  - test: รัน — ลากวาดได้, คลิกบน panel ไม่วาง tile
- [x] 6.3 Shortcut Ctrl+Z / Ctrl+Y
  - test: รัน — ลาก 1 เส้น กด Ctrl+Z 1 ครั้งหายทั้งเส้น, Ctrl+Y กลับมา

## เฟส 7: Palette Panel

- [x] 7.1 Panel ปุ่มสี tile + highlight ตัวเลือก → PaintTool ใช้ tile ที่เลือก
  - test: รัน — เลือกสีแล้ววาดออกมาเป็นสีนั้น

## เฟส 8: Save / Load

- [x] 8.1 `MapDto` + `MapJsonContext` (source gen) + `JsonMapRepository` → หลายไฟล์ `Documents/GameEditor/maps/<name>.json` (เขียน .tmp แล้ว rename)
  - test: `dotnet test` — save แล้ว load ได้แผนที่เดิม (Tests ต้อง ref Infrastructure เพิ่ม)
- [x] 8.2 ไฟล์เสีย/ขนาดไม่ตรง/ดิสก์ error → Result fail + ข้อความใน ImGui (แถบเมนู)
  - test: `dotnet test` — JSON เสีย → fail ไม่ throw; รัน — แก้ map.json ให้พังแล้ว Load เห็นข้อความ ไม่ crash
- [x] 8.3 End-to-end
  - test: รัน — วาด → Save → ปิด → เปิดใหม่ → Load ได้แผนที่เดิม
- [x] 8.4 Save หลายชื่อ: `IMapRepository` `Save(map,name)`/`Load(name)`/`List()`, ชื่อมี `.. \ /` หรืออักขระต้องห้าม → fail, service จำ `CurrentName`
  - test: `dotnet test` — save 2 ชื่อ List เห็นทั้งคู่, `../x` → fail ไม่เขียนนอกโฟลเดอร์
- [x] 8.5 UI: Save (ไม่มีชื่อ → Save As), Save As popup (ชื่อซ้ำถามทับ), Open (เมนูย่อยจาก `maps/`)
  - test: รัน — Save As a/b แล้ว Open สลับได้ภาพถูก, ชื่อซ้ำขึ้นถาม
- [x] 8.6 `IsDirty` + ถาม "Discard unsaved changes?" ก่อน Open/ปิดหน้าต่าง, Esc ไม่ปิด editor
  - test: รัน — วาดแล้วกดปิด/Open ขึ้นถาม, Cancel ไม่เสียงาน

### 🎯 Milestone: Vertical Slice เสร็จ

## เฟส 9: Camera

- [x] 9.1 Pan (ปุ่มกลางเมาส์)
  - test: รัน — ลากเมาส์กลาง map เลื่อนตาม
- [x] 9.2 Pick tile รองรับ pan
  - test: รัน — pan แล้ววาดลงช่องถูก
- [ ] 9.3 Zoom (scroll ซูมเข้าหาเมาส์) — ใช้ `Camera2D`
  - test: รัน — zoom ลื่น จุดใต้เมาส์อยู่ที่เดิม
- [ ] 9.4 Pick tile รองรับ zoom
  - test: รัน — zoom แล้ว highlight/วาดยังตรงช่อง

## เฟส 10: Scene ใน Panel

- [ ] 10.1 วาดฉากลง `RenderTexture2D` แสดงใน panel "Scene"
  - test: รัน — ฉากอยู่ใน panel, ย้าย/ขยาย panel ได้
- [ ] 10.2 แปลงเมาส์ relative กับ panel
  - test: รัน — ย้าย panel แล้ววาด tile ยังตรงช่อง

## เฟส 11: Object + Inspector

- [ ] 11.1 Domain `MapObject` + Command เพิ่ม/ลบ/ย้าย
  - test: `dotnet test` — undo/redo object ถูก
- [ ] 11.2 Depth sort (Y-sort)
  - test: รัน — object ข้างหน้าทับข้างหลังถูก
- [ ] 11.3 Panel Hierarchy + Inspector (แก้ผ่าน Command, undo ตอนเริ่ม/จบลาก)
  - test: รัน — แก้ค่าใน Inspector แล้ว undo 1 ครั้งต่อการลาก

## เฟส 12: Sprite จริง + Asset

- [ ] 12.1 โหลด texture + TileSet (TileId → texture) — PNG โปร่งใส ฐาน 64×32 anchor กึ่งกลางล่าง
  - test: รัน — tile แสดงเป็นภาพ ต่อกันไม่มีรอยรั่ว
- [ ] 12.2 Panel Assets (`ImageButton`)
  - test: รัน — เลือก asset จาก panel แล้ววาดได้

## เฟส 13: Play Mode

- [ ] 13.1 Snapshot ก่อน Play, Stop แล้ว restore
  - test: `dotnet test` — แก้ระหว่าง play แล้ว stop ได้แผนที่เดิม
- [ ] 13.2 ตัวละครเดินบนแผนที่ (input + collision ง่าย)
  - test: รัน — เดินได้ ชนช่อง solid แล้วหยุด

## เฟส 14: Export

- [ ] 14.1 โปรเจกต์ `GameEditor.Player` (ใช้ Domain + Rendering) โหลด `maps/<name>.json` รันเป็นเกม
  - test: `dotnet run --project src/GameEditor.Player` เห็นแผนที่ที่ทำจาก editor
- [ ] 14.2 `dotnet publish` เป็น exe เดียว
  - test: รัน exe จากโฟลเดอร์ publish บนเครื่องได้ ไม่ต้องมี editor

---
name: pr-review
description: ใช้เมื่อ user สั่ง "review PR" / "รีวิวโค้ด" / "รีวิว diff" / "ตรวจโค้ด" / "code review" ให้รีวิวแบบรวมหลายมุม ตอบไทย และไม่แก้โค้ดจนกว่าจะ "ตกลง"
---

# PR Review

## ขั้นตอน
1. หา diff: ถ้ามีเลข PR ใช้ `gh pr diff <n>` ถ้าไม่มีใช้ `git diff` ของ branch ปัจจุบันเทียบกับ base
   ถ้าไม่ใช่ git repo: ให้รีวิวไฟล์ที่ถูกแก้ตาม `TASKS.md` หรือตามที่ user ระบุ ส่วน `/code-review` ให้ส่ง path ไฟล์เหล่านั้นไปเป็น target
2. รัน `/code-review high` แต่ห้ามใส่ `--fix`
3. รัน `/ponytail-review`
4. ถ้า diff แตะ file I/O, network หรือการ parse ไฟล์จากภายนอก ให้รัน `/security-review` ด้วย
5. เช็ค checklist ทั้ง 2 ชุดด้านล่าง

## Checklist: ไฟล์ใน diff
- **ไม่มี BOM**: ทุกไฟล์ใน diff ต้องเป็น UTF-8 ที่ไม่มี BOM คำสั่งนี้ต้องไม่มีผลลัพธ์
  ```bash
  git diff --name-only --diff-filter=d <base> | while read f; do head -c3 "$f" | LC_ALL=C grep -q $'\xEF\xBB\xBF' && echo "$f"; done
  ```
- **Dead code**:
  - function, class หรือ field ที่เพิ่มใหม่ต้องมีที่เรียกใช้ ใช้ grep ชื่อเพื่อเช็คว่ามีผลมากกว่า 1 ที่
  - ห้ามมีโค้ดที่ comment ทิ้งไว้, import ที่ไม่ได้ใช้ หรือ branch ที่ไม่มีทางเข้า
  - ถ้าโค้ดเดิมถูกแทนที่แล้ว ตัวเก่าต้องถูกลบด้วย
- **ไฟล์ขยะและไฟล์ build**: ห้ามมีไฟล์เหล่านี้ใน diff
  - build output: `bin/` `obj/` `build/` `dist/` `Library/` `Temp/` `*.exe` `*.dll` `*.pdb`
  - ไฟล์ขยะ: `*.log` `*.tmp` `*.bak` `*.orig` `Thumbs.db` `.DS_Store` และ `TESTS.md` ที่เหลือจากการเทส
  - ถ้าเจอ: ต้องลบออกจาก diff และเพิ่ม pattern นั้นใน `.gitignore`

## Checklist: game editor
- มี undo/redo สำหรับทุกการแก้ scene หรือ asset
- serialize ได้: field ใหม่ save และ load ได้ และไฟล์เก่ายังเปิดได้
- ไม่มี allocation หรือ I/O ใน update หรือ render loop
- ลบ object แล้วไม่ทิ้ง dangling reference เช่น selection, inspector หรือ child
- path ภาษาไทยหรือมีช่องว่างบน Windows ใช้ได้

## Output (ภาษาไทย)
| ระดับ | ไฟล์:บรรทัด | ปัญหา | วิธีแก้ |
|---|---|---|---|
| 🔴 ต้องแก้ / 🟡 ควรแก้ / 🔵 nit | ... | ... | ... |

ปิดท้ายด้วย verdict: ✅ merge ได้ / 🟡 แก้ก่อน merge / 🔴 ห้าม merge

**ห้ามแก้โค้ด** จนกว่า user จะบอก "ตกลง" ถ้าจะโพสต์ comment ลง PR ต้องถาม user ก่อนทุกครั้ง

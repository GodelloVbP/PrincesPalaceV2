from pathlib import Path
import importlib.util, json
from PIL import Image, ImageDraw, ImageFont, ImageOps
ROOT = Path(__file__).resolve().parents[6]
BASE = ROOT / "Assets/_Project/Art/UI/Status"
spec = importlib.util.spec_from_file_location("green_key", ROOT / "tools/key_green_screen.py")
keyer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(keyer)
font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 13)
report=[]
files=sorted((BASE/"Raw").glob("*.png"))
sheet=Image.new("RGB",(820,70+len(files)*70),"#211822")
d=ImageDraw.Draw(sheet)
d.text((14,10),"STATUS GLYPHS | actual pixel sizes; no enlargement",font=font,fill="white")
for x,label in [(175,"16px"),(235,"24px"),(305,"32px"),(385,"24px grey"),(485,"24px light"),(590,"36px badge"),(695,"56px detail")]:
    d.text((x,40),label,font=font,fill="#d2c4d2")
for i,p in enumerate(files):
    source=Image.open(p)
    rgba=source.convert("RGBA")
    # Preserve native generated transparency. Key only opaque green-backed sources.
    corners=[rgba.getpixel(pos) for pos in [(0,0),(rgba.width-1,0),(0,rgba.height-1),(rgba.width-1,rgba.height-1)]]
    green=all(g-max(r,b)>60 and a>250 for r,g,b,a in corners)
    if green: rgba=keyer.key_out_green(source)
    bbox=rgba.getchannel("A").point(lambda a:255 if a>8 else 0).getbbox()
    if not bbox: raise RuntimeError("Empty image: "+p.name)
    crop=rgba.crop(bbox)
    # Common optical extent; premultiplied resize prevents transparent-edge colour bleed.
    def fit(size):
        target=max(1,round(size*.88))
        ratio=target/max(crop.size)
        tile=crop.convert("RGBa").resize((max(1,round(crop.width*ratio)),max(1,round(crop.height*ratio))),Image.Resampling.LANCZOS).convert("RGBA")
        canvas=Image.new("RGBA",(size,size))
        canvas.alpha_composite(tile,((size-tile.width)//2,(size-tile.height)//2))
        return canvas
    out=fit(256)
    out.save(BASE/"Processed"/p.name)
    def fit(size):
        return out.convert("RGBa").resize((size,size),Image.Resampling.LANCZOS).convert("RGBA")
    for size in [16,24,32,48]:
        folder=BASE/"Review"/str(size);folder.mkdir(exist_ok=True)
        fit(size).save(folder/p.name)
    y=75+i*70
    d.text((14,y+10),p.stem,font=font,fill="white")
    for x,size in [(175,16),(235,24),(305,32),(695,56)]:
        tile=fit(size);sheet.paste(tile,(x,y),tile)
    grey=ImageOps.grayscale(fit(24)).convert("RGBA");grey.putalpha(fit(24).getchannel("A"))
    sheet.paste(grey,(385,y),grey)
    d.rectangle((475,y-5,530,y+40),fill="#e5dbce")
    tile=fit(24);sheet.paste(tile,(485,y),tile)
    benefit=p.stem in ["protect","shielded","empowered","regen","speed"]
    d.rounded_rectangle((590,y-3,625,y+32),radius=6 if benefit else 1,fill="#140a10",outline="#8fbf6a" if benefit else "#e07a62",width=2)
    sheet.paste(tile,(595,y),tile)
    d.rectangle((614,y+19,628,y+34),fill="#140a10");d.text((616,y+17),"2",font=font,fill="white")
    alpha=out.getchannel("A")
    assert alpha.getextrema()==(0,255)
    assert all(out.getpixel(pos)[3]==0 for pos in [(0,0),(255,0),(0,255),(255,255)])
    report.append({"slug":p.stem,"source_size":source.size,"source_mode":source.mode,"background":"green keyed" if green else "native alpha preserved","export_size":out.size,"alpha_bbox":alpha.getbbox()})
sheet.save(BASE/"Review/contact-sheet.png")
(BASE/"Review/verification.json").write_text(json.dumps(report,indent=2))
print(json.dumps(report))

# A backdrop stress sheet, not an in-engine screenshot.
backdrop=Image.open(ROOT/"Assets/_Project/Art/Backgrounds/Fight.png").convert("RGB")
board=ImageOps.fit(backdrop,(840,450))
draw=ImageDraw.Draw(board)
draw.rectangle((0,0,840,58),fill="#211822")
draw.text((16,12),"STATUS ICONS | 24px glyphs in 36px badges on the project's Fight art",font=font,fill="white")
draw.text((16,33),"Art QA composite only; frames and counters are not baked into PNG exports.",font=font,fill="#d2c4d2")
for i,p in enumerate(files):
    x=25+(i%7)*117;y=105+(i//7)*165
    tile=Image.open(BASE/"Processed"/p.name).convert("RGBa").resize((24,24),Image.Resampling.LANCZOS).convert("RGBA")
    draw.rounded_rectangle((x,y,x+35,y+35),radius=5,fill="#140a10",outline="#d2b4c4",width=2)
    board.paste(tile,(x+6,y+4),tile)
    draw.rectangle((x+24,y+23,x+38,y+38),fill="#140a10")
    draw.text((x+26,y+21),"1" if i%2 else "12",font=font,fill="white")
    draw.rectangle((x-4,y+49,x+106,y+72),fill="#211822")
    draw.text((x,y+52),p.stem,font=font,fill="white")
board.save(BASE/"Review/fight-backdrop-check.png")


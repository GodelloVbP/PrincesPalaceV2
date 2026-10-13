import math, random, sys
W,H=1600,900
MX0,MX1=430,1170   # map column inside the screen

def backdrop(seed, eased=0.0, motif=False):
    r=random.Random(seed)
    s=[f'<rect width="{W}" height="{H}" fill="url(#bg)"/>']
    # trees: still, drained
    for i in range(70):
        x=r.uniform(-40,W+40); y=r.uniform(60,H-40); h=r.uniform(60,150)
        a=0.18+0.25*(y/H)
        col='#3a3346' if r.random()<.6 else '#433a50'
        s.append(f'<path d="M{x:.0f} {y:.0f} l{-h*0.22:.0f} {h:.0f} l{h*0.44:.0f} 0 z" fill="{col}" opacity="{a:.2f}"/>')
    for y in (220,470,700):
        s.append(f'<rect x="0" y="{y}" width="{W}" height="38" fill="#cfc8dc" opacity="0.05"/>')
    if motif:
        s.append('<path d="M500 60 C 700 200, 520 340, 760 470 S 900 700, 800 860" stroke="#e8b46a" stroke-width="3" fill="none" opacity="0.55" stroke-dasharray="1 9" stroke-linecap="round"/>')
    return ''.join(s)

def defs():
    return '''<defs>
<linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3b3647"/><stop offset="1" stop-color="#211d29"/></linearGradient>
<radialGradient id="ease" r="0.5"><stop offset="0" stop-color="#f0b868" stop-opacity=".42"/><stop offset=".6" stop-color="#d9a35c" stop-opacity=".12"/><stop offset="1" stop-color="#d9a35c" stop-opacity="0"/></radialGradient>
<radialGradient id="still" r="0.5"><stop offset="0" stop-color="#0d0a12" stop-opacity=".85"/><stop offset="1" stop-color="#0d0a12" stop-opacity="0"/></radialGradient>
<linearGradient id="fog" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#9c94ad" stop-opacity="0"/><stop offset=".5" stop-color="#9c94ad" stop-opacity=".35"/><stop offset="1" stop-color="#9c94ad" stop-opacity="0"/></linearGradient>
</defs>'''

def trail(p,q,state):
    c={'walked':('#e3aa5c',6),'open':('#ddd5ea',4.5),'ahead':('#6a6178',3),'closed':('#4a4356',2)}[state]
    (x1,y1),(x2,y2)=p,q
    my=(y1+y2)/2
    return f'<path d="M{x1} {y1} C {x1} {my}, {x2} {my}, {x2} {y2}" stroke="{c[0]}" stroke-width="{c[1]}" fill="none" stroke-linecap="round"/>'

def node(kind,x,y,sc=1.0,dim=False,pips=0,label=None,cleared=False):
    o=0.45 if dim else 1
    f='#ddd5e8' if not cleared else '#b9a98f'
    st='#15121b'
    r=15*sc
    out=[]
    if kind=='fight': out.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{f}" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='entry': out.append(f'<circle cx="{x}" cy="{y}" r="{r*0.6}" fill="{f}" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='shop': out.append(f'<rect x="{x-r}" y="{y-r}" width="{2*r}" height="{2*r}" fill="{f}" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='event': out.append(f'<rect x="{x-r}" y="{y-r}" width="{2*r}" height="{2*r}" rx="{r*0.5}" fill="{f}" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='rest': out.append(f'<path d="M{x} {y-r*1.2} L{x+r} {y+r*0.8} L{x-r} {y+r*0.8} Z" fill="#ecc890" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='treasure': out.append(f'<path d="M{x} {y-r} L{x+r*0.8} {y} L{x} {y+r} L{x-r*0.8} {y} Z" fill="#e6d29a" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='cache': out.append(f'<rect x="{x-r*0.9}" y="{y-r*0.7}" width="{1.8*r}" height="{1.4*r}" rx="3" fill="#cdb98a" stroke="{st}" stroke-width="2.5" opacity="{o}"/><line x1="{x-r*0.9}" y1="{y-r*0.1}" x2="{x+r*0.9}" y2="{y-r*0.1}" stroke="{st}" stroke-width="2"/>')
    elif kind=='anchor':
        pts=' '.join(f'{x+r*1.15*math.cos(math.pi/6+i*math.pi/3):.1f},{y+r*1.15*math.sin(math.pi/6+i*math.pi/3):.1f}' for i in range(6))
        out.append(f'<polygon points="{pts}" fill="#e0b2b2" stroke="{st}" stroke-width="2.5" opacity="{o}"/>')
    elif kind=='withered':
        pts=' '.join(f'{x+r*1.15*math.cos(math.pi/6+i*math.pi/3):.1f},{y+r*1.15*math.sin(math.pi/6+i*math.pi/3):.1f}' for i in range(6))
        out.append(f'<polygon points="{pts}" fill="#8f8376" stroke="{st}" stroke-width="2.5" stroke-dasharray="4 3"/>')
    for i in range(pips):
        out.append(f'<circle cx="{x+r+6+i*8}" cy="{y-r}" r="3" fill="#e3aa5c" opacity="{o}"/>')
    if label:
        out.append(f'<text x="{x+r+8}" y="{y+r+12}" fill="#a69db6" font-size="13" opacity="{o}">{label}</text>')
    return ''.join(out)

def party(x,y):
    return f'<circle cx="{x}" cy="{y}" r="21" fill="none" stroke="#e3aa5c" stroke-width="3.5"/><path d="M{x-7} {y-38} l7 10 l7 -10 z" fill="#e3aa5c"/>'

def root(p,q,alive=True,frac=1.0):
    (x1,y1),(x2,y2)=p,q
    col='#a33d3d' if alive else '#7a6b5c'
    dash='3 6' if alive else '1 10'
    op=0.95 if alive else 0.5
    return f'<path d="M{x1} {y1} C {x1-60} {(y1+y2)/2}, {x2+(60 if x1<x2 else -60)} {y2-80}, {x2} {y2}" stroke="{col}" stroke-width="3" fill="none" stroke-dasharray="{dash}" opacity="{op}"/>'

def heart(x,y,sc=1.0,anchors=('treant','beetle')):
    s=[]
    s.append(f'<circle cx="{x}" cy="{y}" r="{150*sc}" fill="url(#still)"/>')
    b=60*sc
    s.append(f'<path d="M{x} {y-b} C {x+b*0.9} {y-b}, {x+b*1.15} {y-b*0.2}, {x+b} {y+b*0.4} C {x+b*0.8} {y+b}, {x-b*0.8} {y+b}, {x-b} {y+b*0.4} C {x-b*1.15} {y-b*0.2}, {x-b*0.9} {y-b}, {x} {y-b} Z" fill="#4c4059" stroke="#15121b" stroke-width="3"/>')
    s.append(f'<circle cx="{x-b*0.3}" cy="{y-b*0.15}" r="{5*sc}" fill="#efe0ff"/><circle cx="{x+b*0.3}" cy="{y-b*0.15}" r="{5*sc}" fill="#efe0ff"/>')
    if 'treant' in anchors:  # treant at its left
        tx=x-b*1.9
        s.append(f'<path d="M{tx} {y+b*0.8} l{-18*sc} 0 l{6*sc} {-b*1.3} l{-14*sc} {-14*sc} l{22*sc} {6*sc} l{4*sc} {-12*sc} l{6*sc} {14*sc} l{20*sc} {-8*sc} l{-12*sc} {18*sc} l{6*sc} {b*1.3} z" fill="#56664c" stroke="#15121b" stroke-width="2.5"/>')
    if 'beetle' in anchors:
        bx=x+b*1.9
        s.append(f'<ellipse cx="{bx}" cy="{y+b*0.5}" rx="{30*sc}" ry="{20*sc}" fill="#5c4f45" stroke="#15121b" stroke-width="2.5"/><line x1="{bx}" y1="{y+b*0.5-20*sc}" x2="{bx}" y2="{y+b*0.5+20*sc}" stroke="#15121b" stroke-width="2"/>')
    return ''.join(s)

def hud(floor_txt='The Hush · floor 4', gold='142 gold'):
    return (f'<rect x="0" y="0" width="{W}" height="64" fill="#17141d" opacity=".88"/>'
            f'<text x="40" y="41" fill="#efeaf5" font-size="24" font-family="Cormorant Garamond, Georgia, serif" font-weight="700">{floor_txt}</text>'
            f'<text x="{W-40}" y="41" fill="#e3aa5c" font-size="18" text-anchor="end">{gold}</text>'
            f'<g transform="translate(40,{H-40})"><text fill="#8d849c" font-size="14">HP</text>'
            f'<rect x="30" y="-12" width="160" height="12" rx="3" fill="#3a3346"/><rect x="30" y="-12" width="{{hp}}" height="12" rx="3" fill="#b85c5c"/></g>')

def card(cols, y=H-150, h=110):
    n=len(cols); w=min(1000, 420*n); x0=(W-w)/2
    s=[f'<rect x="{x0}" y="{y}" width="{w}" height="{h}" rx="10" fill="#17141d" stroke="#4d4559" opacity=".95"/>']
    cw=w/n
    for i,(title,lines,accent) in enumerate(cols):
        cx=x0+i*cw+24
        if i: s.append(f'<line x1="{x0+i*cw}" y1="{y+16}" x2="{x0+i*cw}" y2="{y+h-16}" stroke="#4d4559"/>')
        s.append(f'<text x="{cx}" y="{y+32}" fill="{accent}" font-size="17" font-weight="600">{title}</text>')
        for j,l in enumerate(lines):
            s.append(f'<text x="{cx}" y="{y+58+j*22}" fill="#b4abc3" font-size="15">{l}</text>')
    return ''.join(s)

def svg(body, hp=150):
    return f'<svg viewBox="0 0 {W} {H}" xmlns="http://www.w3.org/2000/svg" font-family="Source Sans 3, system-ui, sans-serif">{defs()}{body.replace("{hp}",str(hp))}</svg>'

frames=[]
# ---------- realm geometry for the overview (frame 1) ----------
# overview: whole realm squeezed to 900 tall, spine at x=800
sx=800
ys=[110+i*72 for i in range(9)]  # offsets 0..8, heart below
f1=[backdrop(1)]
# spine trails
for i in range(8): f1.append(trail((sx,ys[i]),(sx,ys[i+1]),'ahead'))
# fork at 2: shop left, fight right
f1+= [trail((sx,ys[1]),(sx-110,ys[2]),'ahead'),trail((sx-110,ys[2]),(sx,ys[3]),'ahead')]
# pocket A: from 3 to 5 on left via 3 rooms
pa=[(sx-250,ys[3]+30),(sx-280,ys[4]),(sx-250,ys[5]-30)]
f1+= [trail((sx,ys[3]),pa[0],'ahead'),trail(pa[0],pa[1],'ahead'),trail(pa[1],pa[2],'ahead'),trail(pa[2],(sx,ys[5]),'ahead')]
# pocket B: 5 to 7 right via 2 rooms
pb=[(sx+240,ys[5]+40),(sx+260,ys[6]+30)]
f1+= [trail((sx,ys[5]),pb[0],'ahead'),trail(pb[0],pb[1],'ahead'),trail(pb[1],(sx,ys[7]),'ahead')]
hx,hy=sx,ys[8]+20
f1.append(root(pa[1],(hx-70,hy),True)); f1.append(root(pb[0],(hx+70,hy),True))
f1.append(heart(hx,hy,0.8))
f1.append(node('entry',sx,ys[0]))
for i,k in [(1,'fight'),(2,'fight'),(3,'fight'),(4,'event'),(5,'fight'),(6,'rest'),(7,'fight')]:
    f1.append(node(k,sx,ys[i],pips=1 if k=='fight' else 0))
f1.append(node('shop',sx-110,ys[2]))
f1.append(node('fight',pa[0][0],pa[0][1],pips=2)); f1.append(node('anchor',*pa[1],pips=3)); f1.append(node('treasure',*pa[2]))
f1.append(node('anchor',*pb[0],pips=3)); f1.append(node('cache',*pb[1]))
f1.append(party(sx,ys[0]))
f1.append(hud())
f1.append(f'<path d="M{sx+330} {hy} C {sx+420} {hy-200}, {sx+420} {ys[0]+200}, {sx+60} {ys[0]+6}" stroke="#efeaf5" stroke-width="2" fill="none" stroke-dasharray="6 6" opacity=".55" marker-end="url(#)"/>')
f1.append(f'<text x="{sx+430}" y="{(hy+ys[0])/2}" fill="#efeaf5" font-size="15" opacity=".8">camera opens on the heart,</text><text x="{sx+430}" y="{(hy+ys[0])/2+20}" fill="#efeaf5" font-size="15" opacity=".8">then rises to the party</text>')
frames.append(svg(''.join(f1),hp=160))

# ---------- frame 2: choose a pocket (close-up) ----------
cx=800; yA=170; yB=330; yC=520
f2=[backdrop(2)]
f2.append('<circle cx="800" cy="90" r="110" fill="url(#ease)"/><circle cx="800" cy="170" r="110" fill="url(#ease)"/>')
f2.append(trail((cx,40),(cx,yA),'walked'))
f2.append(trail((cx,yA),(cx,yB),'open'))
f2.append(trail((cx,yA),(cx-300,yB-20),'open'))
f2.append(trail((cx,yB),(cx,yC),'ahead'))
f2.append(trail((cx-300,yB-20),(cx-360,yC-40),'ahead'))
f2.append(trail((cx-360,yC-40),(cx-300,yC+120),'ahead'))
f2.append(f'<rect x="{cx-470}" y="{yB+30}" width="300" height="300" fill="url(#fog)" opacity="0"/>')
f2.append(node('fight',cx,yB,sc=1.5,pips=2,label='Giant Rat ×2'))
f2.append(node('anchor',cx-300,yB-20,sc=1.5,pips=3,label='Elder Treant'))
f2.append(node('treasure',cx-360,yC-40,sc=1.3,dim=False))
f2.append(node('event',cx,yC,sc=1.3,dim=True))
f2.append(root((cx-300,yB-20),(cx-120,H-170),True))
f2.append(f'<text x="{cx-120}" y="{H-176}" fill="#c47070" font-size="13">↓ root runs to the heart</text>')
f2.append(party(cx,yA))
f2.append(hud())
f2.append(card([('Spine · Giant Rat ×2',['strength ●●  ·  gold + consumable','walking past the pocket: catch breath, +8% HP'],'#efeaf5'),
                ('Pocket · Elder Treant (anchor)',['strength ●●●  ·  spell book, then treasure','+2 rooms  ·  if left alive, it stands with the boss'],'#e0b2b2')]))
frames.append(svg(''.join(f2),hp=118))

# ---------- frame 3: anchor falls ----------
f3=[backdrop(3,eased=1)]
ax,ay=520,300
f3.append(f'<circle cx="{ax}" cy="{ay}" r="190" fill="url(#ease)"/><circle cx="800" cy="150" r="120" fill="url(#ease)"/>')
f3.append(trail((800,30),(800,150),'walked'))
f3.append(trail((800,150),(ax,ay),'walked'))
f3.append(trail((ax,ay),(470,470),'open'))
f3.append(trail((800,150),(800,330),'closed'))
f3.append(node('fight',800,330,sc=1.4,dim=True))
f3.append(root((ax,ay),(760,H-60),False))
f3.append(node('withered',ax,ay,sc=1.5))
f3.append(node('treasure',470,470,sc=1.4))
f3.append(party(ax,ay))
f3.append(hud(gold='171 gold'))
# inset: the heart, treant gone
f3.append(f'<g transform="translate(1180,520)"><rect x="0" y="0" width="360" height="260" rx="10" fill="#17141d" stroke="#4d4559" opacity=".95"/>'
          f'<text x="18" y="30" fill="#a69db6" font-size="14">the heart, far below</text></g>')
f3.append(heart(1385,665,0.62,anchors=('beetle',)))
f3.append(f'<path d="M1240 700 q 20 -40 40 -10 q 15 25 30 -5" stroke="#7a6b5c" stroke-width="3" fill="none" stroke-dasharray="1 7"/>')
f3.append(f'<text x="1198" y="768" fill="#b4abc3" font-size="13">the treant\'s place beside it is empty</text>')
f3.append(f'<text x="{ax-330}" y="{ay+70}" fill="#e3c08a" font-size="15">its root withers back toward the heart</text>')
frames.append(svg(''.join(f3),hp=82))

# ---------- frame 4: the heart ----------
f4=[backdrop(4)]
f4.append(trail((800,40),(800,250),'walked'))
f4.append('<circle cx="800" cy="200" r="120" fill="url(#ease)"/>')
f4.append(root((1150,60),(905,470),True))
f4.append(heart(800,500,1.9,anchors=('beetle',)))
f4.append(party(800,250))
f4.append(hud(gold='171 gold'))
f4.append(card([('The heart · Forest Troll',['with Ironback Beetle (anchor left alive)','the fight: Troll + Beetle  ·  drop: +1 reward for the Beetle'],'#efeaf5')], y=H-150))
frames.append(svg(''.join(f4),hp=70))

# ---------- frame 5: later run ----------
f5=[backdrop(1,motif=True)]
for (x,y,r) in [(sx,ys[0],110),(sx,ys[1],110),(sx-110,ys[2],100),(sx,ys[3],110),(sx-250,ys[3]+30,120),(pa[1][0],pa[1][1],140),(pa[2][0],pa[2][1],120),(sx,ys[5],110)]:
    f5.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="url(#ease)"/>')
f5.append(trail((sx,ys[0]),(sx,ys[1]),'walked'))
for i in (1,2,3,4): f5.append(trail((sx,ys[i]),(sx,ys[i+1]),'closed'))
for i in (5,6,7): f5.append(trail((sx,ys[i]),(sx,ys[i+1]),'open' if i==5 else 'ahead'))
f5+= [trail((sx,ys[1]),(sx-110,ys[2]),'walked'),trail((sx-110,ys[2]),(sx,ys[3]),'walked')]
f5+= [trail((sx,ys[3]),pa[0],'walked'),trail(pa[0],pa[1],'walked'),trail(pa[1],pa[2],'walked'),trail(pa[2],(sx,ys[5]),'walked')]
f5+= [trail((sx,ys[5]),pb[0],'open'),trail(pb[0],pb[1],'ahead'),trail(pb[1],(sx,ys[7]),'ahead')]
f5.append(root(pb[0],(hx+70,hy),True))
f5.append(heart(hx,hy,0.8,anchors=('beetle',)))
f5.append(node('entry',sx,ys[0],cleared=True))
for i,k in [(1,'fight'),(3,'fight')]: f5.append(node(k,sx,ys[i],cleared=True))
f5.append(node('fight',sx,ys[5]))
f5.append(node('fight',sx,ys[2],cleared=True,dim=True)); f5.append(node('event',sx,ys[4],dim=True)); f5.append(node('rest',sx,ys[6])); f5.append(node('fight',sx,ys[7],pips=1))
f5.append(node('shop',sx-110,ys[2],cleared=True))
f5.append(node('fight',*pa[0],cleared=True)); f5.append(node('withered',*pa[1])); f5.append(node('treasure',*pa[2],cleared=True))
f5.append(node('anchor',*pb[0],pips=3)); f5.append(node('cache',*pb[1]))
f5.append(party(sx,ys[5]))
f5.append(hud(floor_txt='The Hush · floor 4 · run 12',gold='96 gold'))
f5.append(f'<text x="{sx-560}" y="{ys[1]}" fill="#e8b46a" font-size="15" opacity=".85">a warm thread, seen in every realm,</text><text x="{sx-560}" y="{ys[1]+20}" fill="#e8b46a" font-size="15" opacity=".85">shows more clearly each run</text>')
frames.append(svg(''.join(f5),hp=104))

open(sys.argv[1],'w').write('\n<!--FRAME-->\n'.join(frames))

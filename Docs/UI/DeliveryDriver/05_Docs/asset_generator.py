from pathlib import Path
import html, math, random, json, os, csv, re
from xml.sax.saxutils import escape
from PIL import Image
import cairosvg
ROOT=Path(__file__).resolve().parents[1]; random.seed(22)
SPRITE_ROOT=ROOT.parents[2]/'Assets/Resources/UI/DeliveryDriver'
W,H=1600,900
C=dict(bg='#09121B', navy='#0C1823',panel='#142530', panel2='#1C303B', edge='#37505B',orange='#FF921E',orangelt='#FFC166', cyan='#52D7DF',mint='#44E4AB',red='#FF6172',yellow='#FFCD65', white='#F6F9FA',muted='#9DB0BA',dim='#607988',violet='#A995F4')
def e(t):return escape(str(t), {'"':'&quot;'})
def svg_start(w=W,h=H,background=True):
 return f'''<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><defs>
  <linearGradient id="night" x2="0.8" y2="1"><stop stop-color="#102F41"/><stop offset=".47" stop-color="#15303F"/><stop offset="1" stop-color="#0B1724"/></linearGradient>
  <linearGradient id="sunset" x2=".2" y2="1"><stop stop-color="#385071"/><stop offset=".52" stop-color="#C46B51"/><stop offset="1" stop-color="#F8B16A"/></linearGradient>
  <linearGradient id="btn" x2="1" y2="1"><stop stop-color="#E97A11"/><stop offset="1" stop-color="#B94D12"/></linearGradient>
  <linearGradient id="glass" x2="1" y2="1"><stop stop-color="#172B3A" stop-opacity=".97"/><stop offset="1" stop-color="#0D1823" stop-opacity=".96"/></linearGradient>
  <linearGradient id="steel" x2="0" y2="1"><stop stop-color="#D6E2E8"/><stop offset=".64" stop-color="#819DAE"/><stop offset="1" stop-color="#546C7F"/></linearGradient>
  <linearGradient id="tarmac" x2="0" y2="1"><stop stop-color="#263E4B"/><stop offset="1" stop-color="#101922"/></linearGradient>
  <filter id="shadow"><feDropShadow dx="0" dy="8" stdDeviation="10" flood-color="#01070B" flood-opacity=".5"/></filter>
  <filter id="soft"><feGaussianBlur stdDeviation="11"/></filter>
  <clipPath id="screenClip"><rect width="1600" height="900" rx="0"/></clipPath>
 </defs>'''
class S:
 def __init__(self,w=W,h=H):self.v=[svg_start(w,h)];self.w=w;self.h=h
 def raw(self,s):self.v.append(s)
 def rect(self,x,y,w,h,fill,rx=0,stroke=None,sw=1,op=1,extra=''):
  self.v.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}" opacity="{op}"'+(f' stroke="{stroke}" stroke-width="{sw}"' if stroke else '')+f' {extra}/>')
 def circle(self,x,y,r,fill,stroke=None,sw=2,opacity=1):
  self.v.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" opacity="{opacity}"'+(f' stroke="{stroke}" stroke-width="{sw}"' if stroke else '')+'/>')
 def line(self,x1,y1,x2,y2,col,sw=2,op=1,dash=''):
  self.v.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{col}" stroke-width="{sw}" opacity="{op}" stroke-linecap="round"'+(f' stroke-dasharray="{dash}"' if dash else '')+'/>')
 def path(self,d,fill='none',stroke=None,sw=2,op=1,extra=''):
  self.v.append(f'<path d="{d}" fill="{fill}" opacity="{op}"'+(f' stroke="{stroke}" stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round"' if stroke else '')+f' {extra}/>')
 def text(self,x,y,t,sz=22,col=None,bold=False,anchor='start',opacity=1,ls=0):
  self.v.append(f'<text x="{x}" y="{y}" font-family="Inter,DejaVu Sans,Arial,sans-serif" font-size="{sz}" font-weight="{750 if bold else 430}" fill="{col or C["white"]}" text-anchor="{anchor}" opacity="{opacity}" letter-spacing="{ls}">{e(t)}</text>')
 def close(self):return ''.join(self.v)+'</svg>'
 def gradient(self,x,y,w,h,g='glass',rx=20,stroke='#38505B'):
  self.rect(x,y,w,h,'url(#'+g+')',rx,stroke,1.3,extra='filter="url(#shadow)"')
 def panel(self,x,y,w,h,title=None,sub=None):
  self.gradient(x,y,w,h)
  if title:
   self.text(x+26,y+42,title.upper(),18,C['white'],True,ls=1)
   if sub:self.text(x+26,y+68,sub,13,C['muted'])
 def icon(self,name,x,y,size=28,color=None):
  color=color or C['orange']
  shapes=ICON_PATHS.get(name,ICON_PATHS['box'])
  self.v.append(f'<g transform="translate({x-size/2},{y-size/2}) scale({size/24})" stroke="{color}" fill="none" stroke-width="1.85" stroke-linecap="round" stroke-linejoin="round">{shapes}</g>')
 def iconbox(self,name,x,y,size=48,color=None):
  self.rect(x,y,size,size,C['panel2'],12, C['edge']); self.icon(name,x+size/2,y+size/2,size*.53,C.get(color,color) if color else None)
 def button(self,x,y,w,h,label,kind='secondary',icon=None,sub=None):
  fill={'primary':'url(#btn)','secondary':'#1B2D38','outline':'#10202B','danger':'#65303A','disabled':'#27333C'}.get(kind,'#1B2D38')
  stroke={'primary':C['orange'],'secondary':'#425965','outline':C['cyan'],'danger':C['red'],'disabled':'#4A5962'}.get(kind,'#425965')
  self.rect(x,y,w,h,fill,13,stroke,1.5)
  xx=x+24
  if icon:self.icon(icon,x+30,y+h/2,26,C['white'] if kind=='primary' else C['orange']);xx=x+58
  self.text(xx,y+(h/2+7 if not sub else h/2-1),label,19,C['white'],True)
  if sub:self.text(xx,y+h/2+22,sub,13,'#D3DFE2' if kind=='primary' else C['muted'])
 def pill(self,x,y,label,typ='mint',w=None):
  col=C[typ];w=w or 24+len(label)*9
  self.rect(x,y,w,30,C['panel2'],9,col,1.2); self.circle(x+13,y+15,4,col); self.text(x+23,y+20,label,13,col,True)
 def bar(self,x,y,w,v,color='orange',h=10):
  self.rect(x,y,w,h,'#2A3A44',h/2)
  self.rect(x,y,max(4,w*max(0,min(1,v))),h,C[color],h/2)
 def divider(self,x,y,w):self.line(x,y,x+w,y,C['edge'],1,.67)
 def metric(self,x,y,name,value,icon='box',color='orange',w=216):
  self.rect(x,y,w,91,'#1B2E39',14,'#354A55'); self.iconbox(icon,x+14,y+18,52,color);self.text(x+78,y+31,name,14,C['muted']);self.text(x+78,y+64,value,25,C[color],True)
 def row(self,x,y,w,name,subtitle='',right='',icon='box',accent='orange',hh=66):
  self.rect(x,y,w,hh,'#1B2C38',12,'#354A56',1)
  self.iconbox(icon,x+10,y+10,hh-20,accent)
  self.text(x+hh+9,y+29,name,18,C['white'],True)
  if subtitle:self.text(x+hh+9,y+49,subtitle,13,C['muted'])
  if right:self.text(x+w-19,y+38,right,17,C[accent],True,'end')
 def top(self,name,kicker='',version='v0.1.3'):
  self.rect(0,0,1600,102,'#08131CD9',0)
  self.rect(56,25,46,46,'#FA901B',12)
  self.icon('truck',79,48,30,C['bg'])
  self.text(116,56,'DELIVERY',27,C['white'],True)
  self.text(270,56,'DRIVER',27,C['orange'],True)
  self.text(58,88,kicker.upper() if kicker else 'LOGISTICS SIMULATOR • UI CONCEPT',12,C['muted'],False,ls=2)
  self.text(1535,43,name,17,C['white'],True,'end')
  self.text(1535,73,version+'  •  CONCEPT UI',12,C['dim'],False,'end')
 def foot(self,hint='ESC  Geri   •   ENTER  Seç   •   TAB  Sonraki'):
  self.rect(0,854,1600,46,'#09141DDC');self.text(65,884,'●  TESLİMAT SÜRÜCÜSÜ  /  DELIVERY DRIVER',12,C['muted'],True)
  self.text(1540,884,hint,12,C['muted'],False,'end')
 def backdrop(self,mode='city'):
  self.rect(0,0,1600,900,'url(#night)')
  if mode=='city':
   self.rect(0,0,1600,530,'url(#sunset)')
   self.circle(1275,255,76,'#FFDC8E',opacity=.43)
   for i in range(34):
    x=i*55-20; height=random.randint(115,385);width=random.randint(42,75)
    self.rect(x,548-height,width,height,random.choice(['#1C3546','#244453','#19323D','#334755']),1)
    for wx in range(int(x+10),int(x+width-4),14):
     for wy in range(int(548-height+15),523,25):
      if random.random()<.4:self.rect(wx,wy,5,9,'#B6B5A0',0,op=.22)
   self.path('M0 615 L1600 535 L1600 900 L0 900 Z','#182632')
   self.path('M705 535 L950 535 L1600 900 L0 900 Z','url(#tarmac)')
   for i in range(7):
    y=574+i**1.7*36
    self.line(822,y,822,y+14+i*6,'#F3C78C',5,0.5)
   # delivery van, ground right
   self.van(1170,555,1.03)
  elif mode=='garage':
   self.rect(0,0,1600,900,'#16232C')
   for i in range(0,1600,250):self.rect(i,115,12,735,'#273744');self.line(i,120,i+120,0,'#4A5B60',9,.4)
   for i in range(4):self.rect(170+i*350,180,235,24,'#E3AD61',6,op=.27)
   self.rect(0,690,1600,210,'#1F2C31')
   for i in range(0,1600,175):self.line(i,690,i-125,900,'#34454D',2,.55)
   self.van(920,450,1.35)
   self.rect(70,460,265,270,'#233641',8);self.rect(95,488,215,175,'#132632',3)
   for i in range(4):self.rect(112+50*i,630,42,42,'#886B4B',3)
  elif mode=='map':
   self.rect(0,0,1600,900,'#0D1C27');
   for i in range(-200,1700,100):
    self.path(f'M{i} 0 L{i+210} 900',stroke='#293D49',sw=30,op=.58)
   for j in range(-100,1000,116):self.line(0,j,1600,j-140,'#2A4050',18,.7)
   for i in range(50,1600,180):
    for j in range(70,900,170):self.rect(i,j,95,65,'#1D3340',6)
   self.path('M330 780 L440 680 L780 645 L815 415 L1120 390',stroke='#FF961D',sw=18,op=.8)
   self.path('M330 780 L440 680 L780 645 L815 415 L1120 390',stroke='#FFDDA1',sw=3)
  self.rect(0,0,1600,900,'#04101B',op=.40)
 def van(self,x,y,s=1):
  self.v.append(f'<g transform="translate({x} {y}) scale({s})">')
  self.path('M15 95 L30 29 Q30 12 51 12 L230 12 Q247 12 249 33 L257 95 L297 105 L299 147 L0 147 L0 113 Z','#CCD7DD',C['edge'],3)
  self.path('M48 29 L102 29 L102 82 L38 82 Z','#294657'); self.path('M115 29 L219 29 L228 82 L115 82 Z','#294657')
  self.path('M32 103 L249 103 L255 118 L26 118 Z',C['orange'])
  self.rect(161,50,49,12,'#ECF1F2',2);self.rect(166,66,45,7,'#C1D0D7',2)
  self.circle(56,149,24,'#101923');self.circle(238,149,24,'#101923');self.circle(56,149,10,'#687A86');self.circle(238,149,10,'#687A86')
  self.rect(266,112,24,13,'#FF6664',3)
  self.v.append('</g>')
 def sidebar(self,title,items,active=None,x=54,y=138,w=272):
  self.gradient(x,y,w,680)
  self.text(x+24,y+43,title.upper(),18,C['white'],True)
  for i,(name,ic) in enumerate(items):
   yy=y+67+i*69
   sel=name==active
   self.rect(x+11,yy,w-22,60,'url(#btn)' if sel else '#1B2D37',12, C['orange'] if sel else '#334D59',1.4)
   self.icon(ic,x+37,yy+30,25,C['white'] if sel else C['muted'])
   self.text(x+66,yy+37,name,17,C['white'] if sel else '#CED9DE',sel)
 def heading(self,title,sub='',x=374,y=162):
  self.text(x,y,title,37,C['white'],True)
  if sub:self.text(x,y+31,sub,16,C['muted'])
 def settings_row(self,x,y,w,name,value,mode='slider',pct=.65):
  self.text(x,y+23,name,18,C['white'],True)
  if mode=='slider':
   self.bar(x+w-315,y+9,205,pct);self.text(x+w-24,y+23,value,16,C['orange'],True,'end')
  elif mode=='switch':
   self.rect(x+w-105,y,80,32,C['mint'] if value=='Açık' else '#374854',17)
   self.circle(x+w-(40 if value=='Açık' else 88),y+16,13,C['white'])
  elif mode=='choice':
   self.rect(x+w-328,y-6,304,44,'#253744',11,'#42606C');self.text(x+w-175,y+23,value,16,C['white'],False,'middle')
  self.divider(x,y+52,w)
 def mini_map(self,x,y,size=250):
  self.rect(x,y,size,size,'#0C2631',20,'#5B7380',2)
  for i in range(0,6):
   self.line(x+12,y+15+i*44,x+size-12,y+5+i*38,'#344D59',9,.73)
   self.line(x+14+i*44,y+5,x+29+i*35,y+size-5,'#34505C',10,.78)
  self.path(f'M{x+54} {y+size-33} L{x+68} {y+size-105} L{x+size-64} {y+size-130} L{x+size-55} {y+50}',stroke=C['orange'],sw=9)
  self.circle(x+size-55,y+49,12,C['orange']);self.circle(x+54,y+size-33,13,C['cyan'])
  self.rect(x+10,y+10,39,33,'#132632',9);self.text(x+29,y+32,'N',17,C['white'],True,'middle')
 def speedometer(self,cx,cy,r=135,value='72',gear='D'):
  self.circle(cx,cy,r,'#112331',C['edge'],4)
  for i in range(23):
   a=(150+i*240/22)*math.pi/180
   p1=(cx+(r-28)*math.cos(a),cy+(r-28)*math.sin(a));p2=(cx+(r-9)*math.cos(a),cy+(r-9)*math.sin(a))
   self.line(*p1,*p2,C['orange'] if i<=14 else '#526A77',3)
  self.text(cx,cy+16,value,82,C['white'],True,'middle')
  self.text(cx,cy+50,'KM/H',17,C['muted'],True,'middle')
  self.rect(cx+83,cy+68,59,44,'#47301F',10,C['orange']);self.text(cx+112,cy+99,gear,26,C['orange'],True,'middle')
 def phone(self,title,app='home',x=1012,y=112,w=360,h=696):
  self.gradient(x,y,w,h,'glass',35,'#69808A')
  self.rect(x+9,y+10,w-18,h-20,'#111E2B',29)
  self.rect(x+w/2-49,y+11,98,13,'#070C12',8)
  self.text(x+32,y+49,'18:45',15,C['white'],True)
  self.text(x+w-35,y+49,'●●●  ▰',12,C['white'],False,'end')
  self.line(x+21,y+68,x+w-21,y+68,'#46606C',1)
  self.icon('arrowleft',x+31,y+105,22,C['orange']);self.text(x+59,y+112,title,22,C['white'],True)
  self.line(x+21,y+130,x+w-21,y+130,'#34505C',1)
  return x+22,y+154,w-44,h-185
ICON_PATHS={
 'box':'<path d="m3 7 9-4 9 4-9 4-9-4Zm0 0v10l9 4 9-4V7M12 11v10"/>',
 'truck':'<rect x="2" y="5" width="13" height="12" rx="2"/><path d="M15 9h4l3 4v4h-7M6 17v1M19 17v1"/><circle cx="7" cy="18" r="2"/><circle cx="19" cy="18" r="2"/>',
 'map':'<path d="m3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2zM9 3v16M15 5v16"/>',
 'pin':'<path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/>',
 'route':'<circle cx="5" cy="18" r="2"/><circle cx="19" cy="6" r="2"/><path d="M7 18h6a5 5 0 0 0 0-10H9"/>',
 'speed':'<path d="M4 18a9 9 0 1 1 16 0M12 15l5-7"/><circle cx="12" cy="15" r="2"/>',
 'fuel':'<path d="M4 21V5a2 2 0 0 1 2-2h7a2 2 0 0 1 2 2v16H4Zm11-12 3 3v5a2 2 0 0 0 4 0V8l-3-3M3 21h13M7 7h5"/>',
 'warning':'<path d="m12 2 10 19H2L12 2Z"/><path d="M12 9v5m0 4h.1"/>',
 'wrench':'<path d="M14 4a7 7 0 0 0-9 9l-3 4a3 3 0 0 0 4 4l5-5a7 7 0 0 0 9-9l-5 5-4-1-1-4z"/>',
 'bank':'<path d="m2 8 10-5 10 5v2H2V8ZM4 20h16M6 10v8m4-8v8m4-8v8m4-8v8M2 22h20"/>',
 'garage':'<path d="M3 21V8l9-5 9 5v13H3ZM7 21V11h10v10M7 15h10"/>',
 'phone':'<rect x="6" y="2" width="12" height="20" rx="3"/><path d="M11 18h2"/>',
 'message':'<path d="M21 11a8 8 0 0 1-8 8H4l2-4a8 8 0 1 1 15-4Z"/><path d="M8 10h8m-8 4h5"/>',
 'settings':'<circle cx="12" cy="12" r="3"/><path d="M9 2h6l1 3 3 2 3-1 2 5-3 2-1 3 1 3-5 3-2-3h-4l-2 3-5-3 1-3-1-3-3-2 2-5 3 1 3-2z"/>',
 'play':'<path d="m7 4 13 8-13 8z"/>','pause':'<path d="M8 4v16M16 4v16"/>',
 'check':'<path d="m3 12 6 6L21 6"/>','close':'<path d="M5 5 19 19M19 5 5 19"/>',
 'star':'<path d="m12 2 3 7 7 .5-5.5 5 1.5 7.5L12 18l-6 4 1.5-7.5L2 9.5 9 9Z"/>',
 'chart':'<path d="M3 3v18h18M7 16v-5m5 5V7m5 9V4"/>',
 'trophy':'<path d="M7 4h10v8a5 5 0 0 1-10 0V4Zm0 2H3v3a4 4 0 0 0 4 4m10-7h4v3a4 4 0 0 1-4 4M12 17v4m-5 0h10"/>',
 'save':'<path d="M4 3h14l3 3v15H3V3h1Zm2 0v6h11V3M7 21v-9h10v9"/>',
 'exit':'<path d="M10 4H4v16h6m5-4 4-4-4-4m-8 4h12"/>',
 'globe':'<circle cx="12" cy="12" r="10"/><path d="M2 12h20m-10-10a16 16 0 0 1 0 20m0-20a16 16 0 0 0 0 20"/>',
 'eye':'<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>',
 'clock':'<circle cx="12" cy="12" r="10"/><path d="M12 5v7l5 3"/>',
 'plus':'<path d="M12 4v16M4 12h16"/>','minus':'<path d="M4 12h16"/>',
 'coin':'<circle cx="12" cy="12" r="10"/><path d="M15 7H9a3 3 0 0 0 0 6h6a3 3 0 0 1 0 6H9m3-15v16"/>',
 'cart':'<path d="M2 4h3l3 12h11l3-9H6M9 21h.1m9 0h.1"/>',
 'users':'<circle cx="9" cy="8" r="4"/><path d="M2 21v-2a7 7 0 0 1 14 0v2M16 4a4 4 0 0 1 0 8m2 4a6 6 0 0 1 4 5"/>',
 'shield':'<path d="m12 2 9 4v6c0 6-4 9-9 10-5-1-9-4-9-10V6l9-4Z"/><path d="m8 12 3 3 5-6"/>',
 'lock':'<rect x="5" y="11" width="14" height="11" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
 'volume':'<path d="M3 9h5l5-5v16l-5-5H3V9ZM17 8a6 6 0 0 1 0 8m2-11a10 10 0 0 1 0 14"/>',
 'keyboard':'<rect x="2" y="5" width="20" height="14" rx="2"/><path d="M5 9h1m3 0h1m3 0h1m3 0h1M5 12h1m3 0h1m3 0h1m3 0h1M7 16h10"/>',
 'gamepad':'<path d="M6 8h12a4 4 0 0 1 4 3l1 6a3 3 0 0 1-5 3l-3-3H9l-3 3a3 3 0 0 1-5-3l1-6a4 4 0 0 1 4-3Zm1 4v5m-2-2h5M17 12h.1m2 3h.1"/>',
 'camera':'<rect x="2" y="5" width="20" height="15" rx="2"/><path d="m8 5 2-3h4l2 3"/><circle cx="12" cy="13" r="4"/>',
 'home':'<path d="m2 11 10-8 10 8M5 10v11h14V10M10 21v-7h4v7"/>',
 'palette':'<path d="M12 2a10 10 0 1 0 0 20h2a3 3 0 0 0 2-5 2 2 0 0 1 2-3h2a4 4 0 0 0 2-4C21 6 17 2 12 2Z"/><circle cx="6" cy="12" r="1"/><circle cx="9" cy="7" r="1"/><circle cx="15" cy="7" r="1"/>',
 'building':'<rect x="4" y="3" width="16" height="19" rx="1"/><path d="M8 7h2m4 0h2M8 11h2m4 0h2M8 15h2m4 0h2M10 22v-4h4v4"/>',
 'info':'<circle cx="12" cy="12" r="10"/><path d="M12 10v7m0-11h.1"/>',
 'download':'<path d="M12 3v13m-5-5 5 5 5-5M4 17v4h16v-4"/>',
 'upload':'<path d="M12 17V4m-5 5 5-5 5 5M4 17v4h16v-4"/>',
 'arrowleft':'<path d="M20 12H4m7-7-7 7 7 7"/>','arrowright':'<path d="M4 12h16m-7-7 7 7-7 7"/>',
 'sliders':'<path d="M4 7h16M4 17h16"/><circle cx="9" cy="7" r="3"/><circle cx="16" cy="17" r="3"/>',
 'spark':'<path d="m12 2 2.5 7.5L22 12l-7.5 2.5L12 22l-2.5-7.5L2 12l7.5-2.5Z"/>',
 'flag':'<path d="M4 22V3m0 1c4-2 6 2 10 0s5-1 6 0v11c-4-2-5 1-9 0s-5 0-7 1"/>',
 'target':'<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="5"/><circle cx="12" cy="12" r="1"/>',
}

SIDESET=[('Genel','settings'),('Grafik','eye'),('Ses','volume'),('Kontroller','gamepad'),('Oynanış','sliders'),('Erişilebilirlik','shield')]
def shell(title,section,background='garage',sidebar=None,active=None):
 s=S();s.backdrop(background);s.top(title,section);s.foot()
 if sidebar:s.sidebar('SEÇENEKLER',sidebar,active)
 return s

def main_menu():
 s=S();s.backdrop('city');s.top('ANA MENÜ','Yolculuğuna başla');s.foot('© HEKYETMISBES  •  UI TASARIM ÖNERİSİ')
 s.text(85,214,'HER TESLİMAT',62,C['white'],True)
 s.text(85,286,'YENİ BİR HİKÂYE.',62,C['orange'],True)
 s.text(91,327,'Kendi lojistik şirketini kur, yollara çık, büyü.',22,C['white'])
 for i,(label,sub,ic) in enumerate([('Oyuna Devam Et','Kaldığın yerden devam et','play'),('Yeni Oyun','Şirketini kur ve yola çık','plus'),('Kayıt Yükle','Son kayıtları görüntüle','save'),('Ayarlar','Oyunu özelleştir','settings'),('Yapımcılar','Emeği geçenler','users'),('Çıkış','Masaüstüne dön','exit')]):
  s.button(85,371+74*i,410,65,label,'primary' if i==0 else 'secondary',ic,sub)
 s.panel(1040,130,477,228,'ŞİRKET PROFİLİ');s.iconbox('building',1065,192,65)
 s.text(1150,224,'Riverside Logistics',26,C['white'],True);s.text(1150,252,'Seviye 3  •  420 / 1.000 XP',16,C['muted']);s.bar(1150,267,315,.42)
 s.metric(1058,300,'BAKİYE','$24.850','coin','mint',215)
 s.metric(1283,300,'AKTİF ARAÇ','Transit 150','truck','orange',215)
 return s

def new_company_setup():
 s=shell('Şirket Kurulumu','İlk giriş • CompanyPageUI','garage');s.heading('Yeni bir başlangıç','Şirketini adlandır, başlangıç aracını seç, ilk teslimata hazırlan.',83,174)
 s.panel(84,239,686,527,'ŞİRKET BİLGİLERİ')
 s.text(112,317,'Şirket adı',18,C['white'],True);s.rect(112,333,625,60,'#263C49',12,'#5D7B87');s.text(132,371,'Riverside Logistics',21,C['white'])
 s.text(112,442,'Başlangıç bütçesi',17,C['muted']);s.text(710,442,'$15.000',30,C['mint'],True,'end')
 s.divider(112,470,615);s.text(112,522,'Şirket logosu',17,C['white'],True)
 for i,(ic,title) in enumerate([('box','Modern'),('truck','Klasik'),('route','Yol')]):
  x=112+i*205;s.rect(x,542,185,100,'#213440',13,C['orange'] if i==0 else C['edge'],2)
  s.icon(ic,x+37,591,37,C['orange']);s.text(x+73,598,title,17,C['white'],True)
 s.panel(796,239,718,527,'İLK ARACINI SEÇ');s.van(868,338,1.65)
 s.pill(849,576,'SEÇİLDİ','mint');s.text(851,637,'Kargo Vanı',24,C['white'],True);s.text(851,667,'Hafif paketler • Düşük yakıt tüketimi',16,C['muted']);s.button(1160,692,309,57,'Şirketi Kur','primary','check')
 return s

def pause_menu():
 s=S();s.backdrop('city');s.rect(0,0,1600,900,'#020912',op=.52);s.top('OYUN DURAKLATILDI','Escape / Pause');s.foot()
 s.panel(425,147,750,660);s.text(800,231,'DURAKLATILDI',45,C['white'],True,'middle');s.text(800,267,'Şehrin seni bekliyor.',18,C['muted'],False,'middle')
 for i,(name,ic,sub) in enumerate([('Devam Et','play','Oyuna dön'),('Kaydet / Yükle','save','İlerlemeni koru'),('Ayarlar','settings','Ses, grafik ve kontroller'),('Ana Menü','home','Ana ekrana dön')]):s.button(508,317+i*99,584,77,name,'primary' if i==0 else 'secondary',ic,sub)
 s.text(801,741,'Bugün 4 teslimat   •   $2.840 kazanç   •   18,6 km',15,C['muted'],False,'middle');return s

def settings_screen(kind):
 active={'settings_general':'Genel','settings_graphics':'Grafik','settings_audio':'Ses','settings_controls':'Kontroller','settings_gameplay':'Oynanış','settings_accessibility':'Erişilebilirlik','accessibility_colorblind':'Erişilebilirlik','localization_preview':'Genel'}.get(kind,'Genel')
 s=shell('AYARLAR','Kişiselleştirme • SettingsMenuUI','garage',SIDESET,active)
 s.heading('Ayarlar  /  '+active,'Değişikliklerin oyuna uygulandığı kontrol merkezi.')
 s.panel(365,225,1168,555)
 x,y,w=407,279,1075
 configs={
 'settings_general':[('Dil','Türkçe','choice',0),('Hız birimi','KM/H','choice',0),('UI ölçeği','110%','slider',.55),('Otomatik kayıt','Açık','switch',0),('Kamera sarsıntısı','45%','slider',.45),('Bildirim süresi','5 saniye','choice',0)],
 'settings_graphics':[('Grafik kalitesi','Yüksek','choice',0),('Çözünürlük','1920 × 1080','choice',0),('Ekran modu','Tam ekran','choice',0),('Gölge kalitesi','80%','slider',.8),('Yansıma kalitesi','65%','slider',.65),('V-Sync','Açık','switch',0)],
 'settings_audio':[('Ana ses','85%','slider',.85),('Müzik','64%','slider',.64),('Efekt sesleri','90%','slider',.9),('Motor sesi','85%','slider',.85),('Arayüz sesleri','70%','slider',.7),('Sessiz mod','Kapalı','switch',0)],
 'settings_controls':[('Gaz','W  /  RT','choice',0),('Fren','S  /  LT','choice',0),('Etkileşim','E  /  A','choice',0),('Telefon','P  /  D-Pad','choice',0),('Harita','M  /  Select','choice',0),('Klavye atamalarını sıfırla','Varsayılan','choice',0)],
 'settings_gameplay':[('Sürüş yardımı','Normal','choice',0),('Kamera tipi','Üçüncü şahıs','choice',0),('Görev hatırlatıcıları','Açık','switch',0),('Mini harita boyutu','120%','slider',.6),('Hassas kargo uyarısı','Açık','switch',0),('İpuçları ve rehber','Açık','switch',0)],
 'settings_accessibility':[('Metin büyüklüğü','125%','slider',.72),('Yüksek kontrast','Açık','switch',0),('Renk körlüğü profili','Kapalı','choice',0),('UI ölçeği','120%','slider',.64),('Alt yazı büyüklüğü','130%','slider',.7),('Tuş ipuçları','Açık','switch',0)],
 'accessibility_colorblind':[('Renk körlüğü modu','Deuteranopia','choice',0),('Semantik ikon desteği','Açık','switch',0),('Yüksek kontrast','Açık','switch',0),('Vurgu parlaklığı','85%','slider',.85),('Yazı büyüklüğü','125%','slider',.7),('Mini harita ölçeği','125%','slider',.72)],
 'localization_preview':[('Aktif dil','Türkçe','choice',0),('İkincil dil','English','choice',0),('Tarih gösterimi','GG.AA.YYYY','choice',0),('Para birimi','USD ($)','choice',0),('Metin uzunluğu testi','Açık','switch',0),('Anlık önizleme','Açık','switch',0)]}
 for i,(label,val,t,v) in enumerate(configs[kind]):s.settings_row(x,y+i*66,w,label,val,t,v)
 s.button(1100,722,190,52,'Varsayılan','secondary','wrench');s.button(1306,722,182,52,'Uygula','primary','check');return s

def save_load():
 s=shell('KAYDET / YÜKLE','SaveLoadUI • JSON kayıtları','garage');s.heading('Kayıt dosyaları','İlerlemeni güvenle sakla ve önceki oturumuna dön.',85,178)
 s.panel(85,240,1430,547,'KAYIT YUVALARI')
 for i,(name,txt,pc,flag) in enumerate([('Otomatik Kayıt','08.10.2026  •  Riverside Logistics','Seviye 3','mint'),('Kayıt 01','07.10.2026  •  24 teslimat','Seviye 2','orange'),('Kayıt 02','05.10.2026  •  11 teslimat','Seviye 1','orange'),('Boş Yuva','Yeni kayıt oluştur','—','muted')]):
  y=320+i*106;s.row(122,y,1355,name,txt,pc,'save',flag,87)
  s.button(1221,y+15,230,54,'Yükle' if i<3 else 'Kaydet','primary' if i==0 else 'secondary','download' if i<3 else 'save')
 return s

def credits():
 s=shell('YAPIMCILAR','CreditsSceneRuntimeUI','city');s.panel(359,163,882,630)
 s.text(800,258,'DELIVERY DRIVER',58,C['white'],True,'middle');s.text(800,302,'BUILD YOUR LOGISTICS COMPANY',18,C['orange'],True,'middle',ls=3)
 s.divider(485,344,630)
 for i,(label,value) in enumerate([('GELİŞTİRİCİ','hekyetmisbes'),('TASARIM','UI konsept paketi'),('MOTOR','Unity'),('TOPLULUK','itch.io oyuncuları')]):
  s.text(800,404+i*80,label,14,C['muted'],True,'middle',ls=2);s.text(800,443+i*80,value,25,C['white'],True,'middle')
 return s

def loading_screen():
 s=S();s.backdrop('city');s.top('YÜKLENİYOR','SceneTransitionManager');s.text(100,611,'ŞEHRE HAZIRLANILIYOR',40,C['white'],True);s.text(100,652,'Yollar, şirketiniz ve teslimatlar yükleniyor…',19,C['muted'])
 s.bar(100,696,1390,.72,'orange',18);s.text(1490,684,'72%',21,C['orange'],True,'end');s.text(100,766,'İPUCU: Hassas kargoyu taşırken sert frenlerden kaçın.',18,C['muted']);return s

def fleet_screen(which):
 items=[('Araçlarım','truck'),('Galeri','cart'),('Yükseltmeler','wrench'),('Şirket Profili','building')]
 active={'fleet_my_vehicles':'Araçlarım','fleet_gallery':'Galeri','fleet_upgrades':'Yükseltmeler','company_profile':'Şirket Profili'}[which]
 s=shell(active,'FleetManagementUI • CompanyPageUI','garage',items,active)
 s.heading('Filo Yönetimi  /  '+active,'Araçların, geliştirmelerin ve işinin merkezine hoş geldin.')
 s.panel(365,223,1168,560)
 if which=='fleet_my_vehicles':
  s.text(404,286,'TRANSIT 150',33,C['white'],True);s.pill(674,257,'AKTİF ARAÇ','mint');s.van(464,360,1.54)
  for i,(n,v,perc) in enumerate([('Araç sağlığı','%93',.93),('Yakıt seviyesi','%72',.72),('Bagaj kapasitesi','12 / 15 m³',.8),('Motor performansı','Seviye 2',.5)]):
   yy=329+i*86;s.text(990,yy,n,17,C['muted']);s.text(1442,yy,v,18,C['white'],True,'end');s.bar(991,yy+16,450,perc,'mint' if i==0 else 'orange')
  s.button(411,687,289,60,'Aktif Araç','primary','check');s.button(719,687,260,60,'Onarım $280','secondary','wrench');s.button(996,687,454,60,'Özelleştir','secondary','palette')
 elif which=='fleet_gallery':
  for i,(name,price,typ) in enumerate([('Transit 150','$11.500','Hafif Van'),('Transit 250','$19.900','Büyük Van'),('Cargo 320','$34.800','Kutu Kamyon')]):
   x=397+i*373;s.rect(x,271,351,465,'#203542',17,C['orange'] if i==1 else C['edge']);s.van(x+17,362,1.00)
   s.text(x+23,564,name,26,C['white'],True);s.text(x+23,595,typ,16,C['muted']);s.text(x+23,639,price,29,C['mint'],True);s.button(x+21,665,306,51,'Satın Al','primary' if i==1 else 'secondary','cart')
 elif which=='fleet_upgrades':
  for i,(name,sub,lv,price,ico) in enumerate([('Motor Gücü','Daha güçlü hızlanma','Seviye 2 / 5','$1.450','speed'),('Fren Sistemi','Daha güvenli duruş','Seviye 1 / 5','$950','wrench'),('Yol Tutuşu','Viraj kontrolünü artır','Seviye 3 / 5','$2.100','route')]):
   yy=272+i*155;s.row(403,yy,1091,name,sub+'  •  '+lv,price,ico,'orange',133);s.bar(499,yy+90,620,(i+1)/5);s.button(1244,yy+45,224,62,'Yükselt','primary','plus')
 else:
  s.iconbox('building',405,289,108);s.text(553,345,'Riverside Logistics',36,C['white'],True);s.text(554,385,'Kuruluş: 08.10.2026   •   Seviye 3',19,C['muted']);s.divider(411,427,1081)
  for i,(n,v,ic,col) in enumerate([('TOPLAM KAZANÇ','$58.420','coin','mint'),('TESLİMAT','47','box','orange'),('MÜŞTERİ PUANI','4.8 / 5','star','yellow'),('SÜRÜŞ MESAFESİ','635 km','route','cyan')]):s.metric(409+(i%2)*539,462+(i//2)*120,n,v,ic,col,506)
 return s

def driving_base():
 s=S();s.backdrop('city')
 s.panel(39,31,442,234)
 s.iconbox('box',59,53,50);s.text(124,74,'AKTİF TESLİMAT',13,C['muted'],True);s.text(124,111,'Hızlı Paket Servisi',25,C['white'],True)
 s.text(66,160,'AL  •  Riverside Depo',18,C['mint'],True);s.text(66,197,'BIRAK  •  Downtown / Merkez',18,C['white'],True)
 s.bar(66,225,378,.45);s.text(437,218,'2/5',15,C['orange'],True,'end')
 s.panel(1097,29,467,153)
 s.icon('coin',1136,74,34,C['mint']);s.text(1167,80,'BAKİYE',14,C['muted'],True);s.text(1167,124,'$24.850',38,C['mint'],True)
 s.icon('fuel',1380,70,29,C['orange']);s.text(1411,77,'%72',24,C['white'],True);s.bar(1361,103,164,.72,'mint')
 s.mini_map(41,585,258)
 s.speedometer(1386,690,137)
 s.rect(592,30,430,97,'#152B38',17,C['edge']);s.icon('route',626,78,45,C['orange']);s.text(677,73,'350 m  •  SOLA DÖN',26,C['white'],True);s.text(677,102,'Maple Caddesi',18,C['muted'])
 s.pill(318,807,'2,4 km  •  8 dk','orange',191)
 return s

def vehicle_screen(which):
 s=driving_base()
 if which=='gameplay_hud':
  s.pill(1117,198,'ARACIN SAĞLIĞI %93','mint',225)
  s.rect(592,779,420,76,'#0E202E',16,C['edge']);s.icon('keyboard',630,817,32);s.text(664,811,'E  Teslim Et',19,C['white'],True);s.text(664,836,'M  Harita  •  P  Telefon',15,C['muted'])
 elif which=='speedometer_detail':
  s.rect(1004,456,575,392,'#0D1A27',23,C['orange']);s.speedometer(1300,647,170,'108','5');s.text(1025,514,'HIZ VE VİTES GÖSTERGESİ',18,C['muted'],True);s.pill(1040,777,'KM/H / MPH','cyan')
 elif which=='reverse_camera':
  s.panel(539,137,527,339,'GERİ GÖRÜŞ KAMERASI','RenderTexture hedef alanı • canlı kamera placeholder')
  s.rect(557,231,490,220,'#273B45',14)
  for i in range(5):
   s.line(590+i*95,430,740+(i-2)*27,282,C['yellow'] if i in (0,4) else C['mint'],4,.77)
  s.line(595,390,1005,390,C['red'],3,.72);s.pill(944,250,'R','red',61)
  s.text(797,474,'Geri viteste görünür • değilken gizlenir',15,C['muted'],False,'middle')
 elif which=='fuel_low_warning':
  s.rect(538,206,531,125,'#53272C',20,C['red'],2)
  s.icon('fuel',606,267,48,C['red']);s.text(655,260,'YAKIT KRİTİK',27,C['white'],True);s.text(655,295,'Depoda yalnızca %9 yakıt kaldı.',17,C['yellow']);s.bar(663,303,322,.09,'red')
  s.pill(536,353,'E  •  YAKIT AL','yellow',245)
 elif which=='fuel_station':
  s.rect(0,0,1600,900,'#040B13',op=.56);s.panel(435,159,732,577,'YAKIT İKMALİ','FuelStationUI • yakın istasyonda E ile açılır')
  s.iconbox('fuel',480,238,73);s.text(580,272,'Mevcut depo',19,C['muted']);s.text(581,308,'%42  /  48 litre',34,C['white'],True);s.bar(486,339,622,.42,'orange',20)
  s.metric(485,390,'LİTRE FİYATI','$2.35 / L','fuel','yellow',295);s.metric(793,390,'BAKİYE','$24.850','coin','mint',322)
  s.button(486,516,623,68,'Yarım Depo Doldur  •  $68','secondary','fuel');s.button(486,599,623,68,'Tam Depo Doldur  •  $136','primary','check')
 elif which=='damage_indicator':
  s.rect(507,207,588,145,'#5F2630',22,C['red'],3);s.icon('warning',569,276,59,C['red']);s.text(632,266,'ÇARPIŞMA TESPİT EDİLDİ',24,C['white'],True)
  s.text(634,309,'ARAÇ SAĞLIĞI: %61',23,C['yellow'],True);s.bar(635,327,403,.61,'red')
  s.rect(1210,451,341,85,'#532833',16,C['red']);s.text(1380,490,'HASAR  −%32',23,C['red'],True,'middle')
 elif which=='world_boundary':
  s.rect(355,156,895,184,'#5D2B30',23,C['red'],2.5);s.icon('warning',425,251,69,C['yellow']);s.text(498,228,'HARİTA SINIRINA YAKLAŞTIN',30,C['white'],True)
  s.text(499,277,'Yola geri dön. 10 saniye sonra araç güvenli alana alınacak.',19,C['yellow']);s.bar(494,300,647,.4,'red')
 elif which=='respawn_transition':
  s.rect(0,0,1600,900,'#03070C',op=.93);s.icon('route',800,355,85,C['orange']);s.text(800,431,'GÜVENLİ NOKTAYA DÖNÜLÜYOR',32,C['white'],True,'middle');s.text(800,467,'Araç en yakın güvenli yola taşınıyor…',18,C['muted'],False,'middle');s.bar(490,528,620,.64,'orange',16)
 return s

def nav_screen(which):
 s=driving_base()
 if which=='minimap_route':
  s.rect(0,0,1600,900,'#06131A',op=.36)
  s.panel(362,139,858,660,'MİNİ HARİTA / ROTA ÖNİZLEMESİ','MinimapCamera RenderTexture yerleşimi')
  s.mini_map(479,246,565)
  s.pill(952,269,'HEDEF: TESLİMAT','orange',210);s.text(966,330,'Downtown / Merkez',20,C['white'],True);s.text(966,376,'Kalan mesafe',15,C['muted']);s.text(966,417,'2,4 KM',34,C['orange'],True);s.text(966,484,'Tahmini süre',15,C['muted']);s.text(966,523,'8 DAKİKA',27,C['white'],True)
 elif which=='compass_navigation':
  s.rect(389,178,828,118,'#101F2B',16,C['orange']);s.text(407,218,'PUSULA • CompassUI',16,C['muted'],True)
  for i,ch in enumerate(['W','NW','N','NE','E']):
   x=481+i*150;s.text(x,261,ch,23,C['orange'] if ch=='N' else C['white'],True,'middle');s.line(x-54,270,x+54,270,C['edge'],2)
  s.path('M797 288l-17 23h34z',C['orange']);s.pill(934,198,'HEDEF  •  2.4 KM','mint',231)
 elif which=='full_navigation_map':
  s=S();s.backdrop('map');s.top('TAM HARİTA','Nav UI • route preview');s.foot();s.panel(42,154,367,578,'AKTİF ROTA')
  for i,(n,t,ic) in enumerate([('Başlangıç','Riverside Depo','garage'),('Teslimat','Downtown / Merkez','pin'),('Tahmini mesafe','2.4 km','route'),('Tahmini varış','8 dakika','clock')]):s.row(59,230+i*111,335,n,t,'',ic,'orange',93)
  s.panel(1107,179,426,172,'HARİTA FİLTRELERİ');s.pill(1137,254,'GÖREV İŞARETLERİ','orange',240);s.pill(1137,294,'AKARYAKIT','mint',155)
 return s

def quest_shell(title,subtitle='TESLİMAT SİSTEMİ'):
 s=shell(title,subtitle,'garage');s.heading(title,subtitle,82,164);return s

def quests(which):
 if which=='active_quest':
  s=driving_base();s.panel(1002,208,543,272,'AKTİF GÖREVİM','ActiveQuestUI')
  s.text(1042,304,'Kargoyu adrese teslim et',23,C['white'],True);s.pill(1042,330,'06:45 KALDI','yellow',175)
  s.text(1045,417,'Kargo dayanıklılığı',16,C['muted']);s.bar(1045,440,449,.84,'mint',15);s.text(1494,414,'%84',17,C['mint'],True,'end');return s
 if which=='mission_offer':
  s=driving_base();s.rect(0,0,1600,900,'#02111C',op=.57);s.panel(475,130,658,638,'YENİ İŞ TEKLİFİ','PhoneMissionUI • karar ekranı')
  s.iconbox('box',519,222,82);s.text(629,259,'Hassas Elektronik Taşıma',26,C['white'],True);s.text(629,287,'Riverside → Downtown',17,C['muted'])
  for i,(n,v,ic) in enumerate([('Kargo türü','Elektronik','box'),('Tahmini süre','11 dakika','clock'),('Tahmini mesafe','4,2 km','route'),('Teklif edilen ücret','$620','coin')]):s.row(515,333+i*70,574,n,v,'',ic,'mint' if i==3 else 'orange',61)
  s.button(515,631,274,71,'Reddet','secondary','close');s.button(805,631,282,71,'Kabul Et','primary','check');return s
 if which=='quest_board':
  s=quest_shell('Teslimat Panosu','QuestListUI + QuestEntryUI');s.panel(80,231,1430,556,'YENİ TESLİMATLAR')
  for i,(name,sub,price,diff) in enumerate([('Ofis Malzemeleri','Downtown  •  2,4 km','$310','Kolay'),('Elektronik Taşıma','Harbor  •  4,2 km','$620','Orta'),('Mobilya Sevkiyatı','West End  •  7,6 km','$850','Zor'),('Gıda Paketi','Merkez  •  3,1 km','$420','Kolay')]):
   yy=299+i*109;s.row(111,yy,1369,name,sub+'  •  '+diff,'','box','mint' if i==0 else 'orange',91);s.text(1178,yy+52,price,22,C['mint'],True,'end');s.button(1240,yy+16,210,59,'Teklifi Gör','primary' if i==0 else 'secondary','arrowright')
  return s
 if which in ('quest_success','quest_failure'):
  success=which=='quest_success';s=S();s.backdrop('city');s.rect(0,0,1600,900,'#07121B',op=.75)
  s.panel(420,118,762,688,'TESLİMAT SONUCU');s.icon('trophy' if success else 'warning',800,236,108,C['mint'] if success else C['red'])
  s.text(800,328,'TESLİMAT TAMAMLANDI!' if success else 'TESLİMAT BAŞARISIZ',33,C['mint'] if success else C['red'],True,'middle')
  s.text(800,372,'Zamanında ve hasarsız teslimat.' if success else 'Süre doldu ya da kargo çok hasar gördü.',18,C['muted'],False,'middle')
  for i,(n,v) in enumerate([('Temel ödül','$480' if success else '$0'),('Süre bonusu','+$120' if success else '$0'),('Hasar kesintisi','$0' if success else '-$90'),('Deneyim','+180 XP' if success else '+25 XP')]):
   yy=412+i*62;s.text(500,yy,n,20,C['muted']);s.text(1100,yy,v,22,C['mint'] if success else C['red'],True,'end');s.divider(500,yy+15,600)
  s.button(516,696,578,65,'Oyuna Dön','primary','play');return s
 if which=='quest_statistics':
  s=quest_shell('Teslimat İstatistikleri','QuestStatisticsUI • SimpleBarChart');s.panel(80,230,917,555,'GÜNLÜK TESLİMAT PERFORMANSI');s.panel(1013,230,500,555,'KARGO DAĞILIMI')
  vals=[4,9,6,11,14,10,17]
  for i,n in enumerate(vals):
   x=149+i*117;barh=n*21;s.rect(x,681-barh,58,barh,C['orange'] if i==6 else '#3F91A0',8);s.text(x+29,707,['Pzt','Sal','Çar','Per','Cum','Cmt','Paz'][i],16,C['muted'],True,'middle');s.text(x+29,673-barh,str(n),16,C['white'],True,'middle')
  for i,(name,val,col) in enumerate([('Elektronik',.83,'orange'),('Gıda',.64,'mint'),('Mobilya',.38,'cyan'),('Ofis',.53,'violet'),('Diğer',.22,'yellow')]):
   yy=323+i*86;s.text(1053,yy,name,18,C['white'],True);s.bar(1054,yy+17,408,val,col,16);s.text(1474,yy-1,str(int(val*100))+'%',18,C[col],True,'end')
  return s
 if which=='tutorial_highlight':
  s=driving_base();s.rect(0,0,1600,900,'#01050C',op=.63)
  s.rect(32,578,278,277,'none',20,C['orange'],5)
  s.path('M480 638L314 694L347 718',stroke=C['orange'],sw=7)
  s.panel(366,329,695,254,'ÖĞRETİCİ  •  2 / 6');s.text(401,414,'Mini haritayı takip et',29,C['white'],True);s.text(402,454,'Turuncu çizgi aktif teslimat rotanı gösterir.',18,C['muted']);s.button(401,496,195,59,'Geç','secondary','close');s.button(612,496,400,59,'İleri','primary','arrowright');return s
 raise KeyError(which)

def phone_screen(which):
 s=driving_base();s.rect(0,0,1600,900,'#06111A',op=.54)
 names={'smartphone_home':'Telefon','smartphone_deliveries':'Kurye Express','smartphone_bank':'QuickPay Banka','smartphone_garage':'Garajım & Filo','smartphone_messages':'Mesajlar & Yorumlar','smartphone_notification':'Bildirimler'}
 app=which.replace('smartphone_','')
 x,y,w,h=s.phone(names[which],app)
 if app=='home':
  s.text(x+7,y+39,'Günaydın, sürücü!',24,C['white'],True);s.text(x+6,y+69,'4 aktif uygulama',15,C['muted'])
  for i,(label,ic,col) in enumerate([('Kurye Express','box','orange'),('QuickPay','bank','mint'),('Garajım','garage','cyan'),('Mesajlar','message','violet')]):
   cx=x+(i%2)*160+9;cy=y+100+(i//2)*159;s.rect(cx,cy,141,139,'#1D3441',21,C['edge']);s.iconbox(ic,cx+38,cy+16,69,col);s.text(cx+72,cy+114,label,15,C['white'],True,'middle')
  s.metric(x+4,y+443,'GÜNLÜK KAZANÇ','$2.840','coin','mint',w-8)
 elif app=='deliveries':
  s.text(x+9,y+28,'GÜNCEL TEKLİFLER',17,C['white'],True)
  for i,(n,sub,price) in enumerate([('Elektronik paket','Downtown • 4,2 km','$620'),('Ofis kolisi','Merkez • 2,4 km','$310'),('Gıda teslimatı','Harbor • 3,1 km','$420')]):
   yy=y+54+i*146;s.rect(x+4,yy,w-8,135,'#1D3441',15,C['edge']);s.icon('box',x+27,yy+31,30);s.text(x+55,yy+38,n,16,C['white'],True);s.text(x+20,yy+71,sub,14,C['muted']);s.text(x+22,yy+104,price,20,C['mint'],True);s.pill(x+179,yy+88,'KABUL ET','orange',113)
 elif app=='bank':
  s.rect(x+3,y+9,w-6,184,'#1C5553',20);s.text(x+28,y+44,'QUICKPAY • HESABIM',15,C['white'],True)
  s.text(x+28,y+105,'$24.850',45,C['white'],True);s.text(x+29,y+144,'Kullanılabilir bakiye',15,'#D7F5EB')
  for i,(n,v,ic) in enumerate([('Toplam kazanç','$58.420','coin'),('Bugünkü kazanç','+$2.840','chart'),('Kurye seviyesi','SEVİYE 3','trophy')]):s.row(x+4,y+224+i*110,w-8,n,'QuickPay özeti',v,ic,'mint',91)
 elif app=='garage':
  s.rect(x+2,y+12,w-4,192,'#1B3442',19);s.van(x+17,y+29,.94)
  s.text(x+15,y+238,'TRANSIT 150',22,C['white'],True)
  for i,(name,val,color,pc) in enumerate([('Yakıt','%72','orange',.72),('Araç sağlığı','%93','mint',.93),('Sürüş mesafesi','12.480 km','cyan',.65),('Yükseltme seviyesi','2 / 5','violet',.4)]):
   yy=y+267+i*83;s.text(x+11,yy,name,16,C['muted']);s.text(x+w-13,yy,val,17,C[color],True,'end');s.bar(x+10,yy+15,w-23,pc,color,11)
 elif app=='messages':
  s.text(x+15,y+29,'MÜŞTERİ PUANI',16,C['muted'],True);s.text(x+w-14,y+32,'4.8 / 5 ★',22,C['yellow'],True,'end')
  for i,(who,msg,side) in enumerate([('Ayşe K.','Paket çok hızlı geldi, teşekkürler!',0),('Mert D.','Kargo sorunsuz teslim edildi.',1),('Zeynep B.','Nazik ve özenli teslimat!',0),('Sistem','Yeni bir değerlendirme aldınız.',1)]):
   yy=y+66+i*121;s.rect(x+4+(28 if side else 0),yy,w-38,100,'#23515B' if side else '#1B3340',15)
   s.text(x+21+(28 if side else 0),yy+29,who,15,C['orange'],True)
   s.text(x+21+(28 if side else 0),yy+55,msg[:34],13,C['white']);s.text(x+21+(28 if side else 0),yy+77,msg[34:66],13,C['white'])
 elif app=='notification':
  for i,(head,sub,ic,acc) in enumerate([('Yeni iş teklifi','Elektronik teslimatı','box','orange'),('Yeni yorum','Müşteri puanı +0.1','star','yellow'),('Depo bildirimi','Depo kapasitesi %80','warning','red'),('Günlük hedef','4 / 5 teslimat','target','mint')]):s.row(x+1,y+28+i*121,w-2,head,sub,'',ic,acc,106)
 if which=='smartphone_notification':
  s.rect(103,683,524,104,'#132A38',20,C['orange'],2);s.icon('phone',147,733,45);s.text(191,730,'YENİ İŞ TEKLİFİ',21,C['white'],True);s.text(191,760,'Açmak için tıkla  →  Kurye Express',15,C['muted'])
 return s

def skill_tree():
 s=shell('BECERİ AĞACI','ProgressionSkillTreeUI • K tuşu','garage');s.heading('Sürücü Becerileri','Teslimatlardan elde ettiğin puanlarla uzmanlığını artır.',85,169)
 s.panel(80,235,1432,556);s.pill(1173,263,'3 BECERİ PUANI','orange',265)
 points=[(328,361,'Yakıt Ustası','fuel','mint'),(735,361,'Hızlı Teslimat','clock','orange'),(1126,361,'Kargo Koruması','shield','cyan'),(328,604,'Ekstra Kazanç','coin','yellow'),(735,604,'Yeni Rotalar','route','violet'),(1126,604,'Usta Sürücü','trophy','orange')]
 for i in range(3):s.path(f'M{points[i][0]} 412 L{points[i+3][0]} 552',stroke=C['edge'],sw=7)
 for x,y,name,ic,color in points:
  s.circle(x,y,66,'#1E3945',C[color],4);s.icon(ic,x,y,64,C[color]);s.text(x,y+99,name,21,C['white'],True,'middle');s.pill(x-58,y+115,'1 / 3' if y==361 else '0 / 3',color,116)
 return s

def overlay_screen(which):
 if which=='balance_reward':
  s=driving_base();s.rect(40,271,435,99,'#133E36',19,C['mint']);s.icon('coin',91,320,53,C['mint']);s.text(139,314,'TESLİMAT ÖDÜLÜ',17,C['muted'],True);s.text(139,351,'+$480',33,C['mint'],True);return s
 if which=='level_up':
  s=driving_base();s.rect(0,0,1600,900,'#050F1A',op=.75)
  for i in range(52):
   x=random.randint(240,1340);y=random.randint(110,770);s.rect(x,y,random.randint(4,15),random.randint(4,15),random.choice([C['orange'],C['mint'],C['cyan'],C['yellow']]),2,op=.66)
  s.panel(411,192,770,503);s.icon('trophy',795,301,105,C['orange']);s.text(798,409,'SEVİYE ATLADIN!',48,C['white'],True,'middle');s.text(795,476,'SEVİYE 4',43,C['orange'],True,'middle');s.text(795,527,'+1 Beceri Puanı  •  Yeni işler açıldı',21,C['muted'],False,'middle');s.button(548,597,506,67,'Devam Et','primary','check');return s
 if which=='achievement_unlocked':
  s=driving_base();s.rect(1071,192,478,163,'#162C37',20,C['yellow'],2);s.iconbox('trophy',1093,224,75,'yellow');s.text(1187,240,'BAŞARIM KAZANILDI',16,C['yellow'],True);s.text(1187,276,'İlk 50 Teslimat',24,C['white'],True);s.text(1187,307,'Ödül: +250 XP',17,C['mint'],True);return s
 if which=='notification_center':
  s=driving_base()
  for i,(title,detail,ic,color) in enumerate([('Yeni iş teklifi','Elektronik paket • $620','box','orange'),('Kargo hasar aldı','Kargo sağlığı: %74','warning','red'),('Yeni seviye','+1 Beceri Puanı','trophy','mint')]):
   yy=210+i*107;s.row(1067,yy,473,title,detail,'',ic,color,92)
  return s
 if which=='confirmation_dialog':
  s=driving_base();s.rect(0,0,1600,900,'#000611',op=.74);s.panel(463,235,675,436,'ONAY GEREKİYOR')
  s.icon('warning',800,361,77,C['yellow']);s.text(800,435,'Oyundan çıkmak istiyor musun?',26,C['white'],True,'middle');s.text(800,474,'Kaydedilmemiş ilerlemen kaybolabilir.',17,C['muted'],False,'middle');s.button(514,541,269,71,'Vazgeç','secondary','close');s.button(800,541,289,71,'Evet, Çık','danger','exit');return s
 if which=='tooltip_state':
  s=driving_base();s.rect(1117,450,404,105,'#172A35',15,C['cyan']);s.icon('info',1150,491,26,C['cyan']);s.text(1182,486,'Yakıt Verimliliği',19,C['white'],True);s.text(1181,516,'Her 100 km için yakıt tüketimi.',15,C['muted']);return s
 if which=='neighborhood_banner':
  s=driving_base();s.rect(527,174,560,138,'#112637',20,C['edge']);s.text(807,217,'YENİ BÖLGEYE GİRDİN',17,C['orange'],True,'middle',ls=2);s.text(807,270,'DOWNTOWN',42,C['white'],True,'middle');return s
 if which=='delivery_world_marker':
  s=driving_base();s.circle(845,448,86,'#203C43',C['orange'],6,.85);s.icon('pin',846,438,68,C['orange']);s.text(845,543,'TESLİMAT NOKTASI',21,C['white'],True,'middle');s.pill(770,557,'E  •  TESLİM ET','orange',194);return s
 if which=='cinematic_capture':
  s=driving_base();s.rect(0,0,1600,900,'#020A0E',op=.32)
  s.panel(61,285,449,493,'SİNEMATİK ÇEKİM','CinematicCaptureHUD')
  for i,(label,key,ic) in enumerate([('Kamera açısı','F7','camera'),('Yavaş çekim','F8','clock'),('Görüntü formatı 16:9 / 9:16','F9','sliders'),('Temiz görüntü (HUD gizle)','F10','eye')]):s.row(82,356+i*91,404,label,'',key,ic,'cyan',77)
  s.rect(606,173,829,469,'none',5,C['white'],3);s.rect(619,187,803,442,'none',4,C['white'],1,op=.48)
  return s
 raise KeyError(which)

SCREENS=[
 ('01_Menus','main_menu','Ana Menü','MainMenuRuntimeUI'),('01_Menus','pause_menu','Duraklatma','PauseMenuUI;PauseMenuRuntimeView;PauseMenuRuntimeViewBuilder'),
 ('01_Menus','settings_general','Genel Ayarlar','SettingsMenuUI'),('01_Menus','settings_graphics','Grafik Ayarları','SettingsMenuUI'),('01_Menus','settings_audio','Ses Ayarları','SettingsMenuUI'),('01_Menus','settings_controls','Tuş Atamaları','SettingsMenuUI'),('01_Menus','settings_gameplay','Oynanış Ayarları','SettingsMenuUI'),('01_Menus','settings_accessibility','Erişilebilirlik','AccessibilityManager'),('01_Menus','save_load','Kayıt Yükleme','SaveLoadUI'),('01_Menus','loading_screen','Yükleme','LoadingScreenUI;SceneTransitionManager'),('01_Menus','credits','Yapımcılar','CreditsSceneRuntimeUI'),
 ('02_Vehicle','gameplay_hud','Ana Sürüş HUD','GameplayHUD;BalanceHudUI;FuelHudUI;SpeedometerUI'),('02_Vehicle','speedometer_detail','Hız Göstergesi','SpeedometerUI'),('02_Vehicle','reverse_camera','Geri Görüş','ReverseCameraHUD'),('02_Vehicle','fuel_low_warning','Düşük Yakıt Uyarısı','FuelHudUI'),('02_Vehicle','fuel_station','Yakıt İkmali','FuelStationUI'),('02_Vehicle','damage_indicator','Çarpışma Hasarı','DamageIndicatorHUD'),('02_Vehicle','world_boundary','Harita Sınırı','BorderRespawnGuard'),('02_Vehicle','respawn_transition','Güvenli Konuma Dönüş','BorderRespawnGuard'),
 ('03_Navigation','minimap_route','Mini Harita','MinimapUI'),('03_Navigation','compass_navigation','Pusula','CompassUI'),('03_Navigation','full_navigation_map','Tam Harita (öneri)','NavigationService'),
 ('04_Quests','active_quest','Aktif Görev','ActiveQuestUI;DeliveryUI'),('04_Quests','mission_offer','Görev Teklifi','PhoneMissionUI'),('04_Quests','quest_board','Görev Panosu','QuestListUI;QuestEntryUI'),('04_Quests','quest_success','Görev Başarısı','QuestCompleteUI'),('04_Quests','quest_failure','Görev Başarısızlığı','QuestCompleteUI'),('04_Quests','quest_statistics','Görev İstatistikleri','QuestStatisticsUI;SimpleBarChart'),('04_Quests','tutorial_highlight','Öğretici','TutorialUI'),
 ('05_Company','new_company_setup','Şirket Kurulumu','CompanyPageUI'),('05_Company','fleet_my_vehicles','Araçlarım','FleetManagementUI'),('05_Company','fleet_gallery','Araç Galerisi','FleetManagementUI'),('05_Company','fleet_upgrades','Araç Yükseltme','FleetManagementUI'),('05_Company','company_profile','Şirket Profili','CompanyPageUI;AutoSignTextSetter'),
 ('06_Smartphone','smartphone_home','Telefon Ana Ekranı','SmartphoneUI;SmartphoneIconRegistry'),('06_Smartphone','smartphone_deliveries','Kurye Express','SmartphoneDeliveryApp'),('06_Smartphone','smartphone_bank','QuickPay','SmartphoneBankApp'),('06_Smartphone','smartphone_garage','Garajım & Filo','SmartphoneGarageApp'),('06_Smartphone','smartphone_messages','Mesajlar','SmartphoneMessagesApp'),('06_Smartphone','smartphone_notification','Telefon Bildirimleri','SmartphoneNotificationToast'),
 ('07_Progression','balance_reward','Bakiye Değişimi','BalanceHudUI'),('07_Progression','level_up','Seviye Atlama','ProgressionUI'),('07_Progression','skill_tree','Yetenek Ağacı','ProgressionSkillTreeUI'),('07_Progression','achievement_unlocked','Başarım','AchievementNotificationUI'),
 ('08_World_System','notification_center','Bildirim Kuyruğu','NotificationQueue'),('08_World_System','confirmation_dialog','Onay Diyaloğu','ConfirmationDialog'),('08_World_System','tooltip_state','Bilgi İpucu','TooltipUI'),('08_World_System','neighborhood_banner','Mahalle Adı','NeighborhoodUI'),('08_World_System','delivery_world_marker','Dünya Teslimat İşareti','DeliveryIndicator'),('08_World_System','cinematic_capture','Sinematik HUD','CinematicCaptureHUD'),('08_World_System','accessibility_colorblind','Renk Körlüğü Kontrolü','AccessibilityManager'),('08_World_System','localization_preview','TR/EN Yerelleştirme','LocalizationTable')
]
def generate_screen(key):
 if key=='main_menu':return main_menu()
 if key=='new_company_setup':return new_company_setup()
 if key=='pause_menu':return pause_menu()
 if key.startswith('settings_') or key in ('accessibility_colorblind','localization_preview'):return settings_screen(key)
 if key=='save_load':return save_load()
 if key=='credits':return credits()
 if key=='loading_screen':return loading_screen()
 if key.startswith('fleet_') or key=='company_profile':return fleet_screen(key)
 if key in ('gameplay_hud','speedometer_detail','reverse_camera','fuel_low_warning','fuel_station','damage_indicator','world_boundary','respawn_transition'):return vehicle_screen(key)
 if key in ('minimap_route','compass_navigation','full_navigation_map'):return nav_screen(key)
 if key in ('active_quest','mission_offer','quest_board','quest_success','quest_failure','quest_statistics','tutorial_highlight'):return quests(key)
 if key.startswith('smartphone_'):return phone_screen(key)
 if key=='skill_tree':return skill_tree()
 return overlay_screen(key)
COMPONENTS={
 'button_primary':(488,116,'button',C['orange']),'button_secondary':(488,116,'button','#203846'),
 'button_outline':(488,116,'button','#112331'),'button_disabled':(488,116,'button','#334149'),
 'button_danger':(488,116,'button','#75343D'),'button_focus':(488,116,'button','#235060'),
 'panel_large':(896,512,'panel','#172B37'),'panel_medium':(640,380,'panel','#172B37'),
 'panel_small':(384,192,'panel','#172B37'),'card_default':(488,234,'panel','#1C333F'),
 'card_selected':(488,234,'panel','#2B3D41'),'card_alert':(488,234,'panel','#402C36'),
 'input_default':(500,80,'input','#1D3540'),'input_focus':(500,80,'input','#1D3540'),
 'tab_idle':(260,78,'button','#1A2F3A'),'tab_active':(260,78,'button','#9F4E12'),
 'tooltip':(484,138,'panel','#1B3441'),'modal_window':(660,510,'panel','#152B37'),
 'toast_default':(620,156,'panel','#1A3440'),'toast_success':(620,156,'panel','#164439'),
 'toast_warning':(620,156,'panel','#53361E'),'toast_danger':(620,156,'panel','#502B34'),
 'badge_success':(230,54,'badge','#23483F'),'badge_warning':(230,54,'badge','#543E26'),
 'badge_danger':(230,54,'badge','#542D38'),'badge_info':(230,54,'badge','#234957'),
 'progress_track':(520,32,'track','#31404B'),'progress_fill_orange':(520,32,'track',C['orange']),
 'progress_fill_mint':(520,32,'track',C['mint']),'progress_fill_red':(520,32,'track',C['red']),
 'progress_fill_cyan':(520,32,'track',C['cyan']),
 'slider_track':(520,23,'track','#334A57'),'slider_thumb':(80,80,'circle',C['white']),
 'toggle_on':(160,74,'toggle',C['mint']),'toggle_off':(160,74,'toggle','#4E616C'),
 'phone_shell':(420,820,'phone','#132534'),
 'hud_stat_tile':(340,126,'panel','#1B333F'),
 'minimap_frame':(400,400,'panel','#102733'),
 'nav_banner':(740,128,'panel','#1E3542'),
 'speedometer_dial':(420,420,'dial','#172735'),
 'world_marker_pickup':(190,235,'marker',C['cyan']),
 'world_marker_dropoff':(190,235,'marker',C['orange']),
 'world_marker_warning':(190,235,'marker',C['red']),
 'divider_line':(550,8,'track','#3B5761'),
 'keycap':(84,76,'button','#233746'),
 'notification_dot':(56,56,'circle',C['orange'])
}
def component_svg(name):
 w,h,t,color=COMPONENTS[name];s=S(w,h);s.raw('')
 if t in ('button','input','panel','badge','track'):
  radius={'button':16,'input':13,'panel':23,'badge':25,'track':min(w,h)/2}[t]
  s.rect(4,4,w-8,h-8,color,radius,'#FF921E' if name in ('button_primary','button_focus','card_selected','tab_active','input_focus') else '#466575' if t!='track' else None, 3 if name in ('button_focus','input_focus') else 2)
  if t=='panel':s.line(32,23,w-32,23,'#7CB4C7',2,.12)
 elif t=='toggle':
  s.rect(6,8,w-12,h-16,color,h/2);s.circle(w-42 if name=='toggle_on' else 43,h/2,28,C['white'])
 elif t=='circle':s.circle(w/2,h/2,min(w,h)*.39,color)
 elif t=='phone':
  s.rect(4,4,w-8,h-8,color,57,'#678490',5);s.rect(20,20,w-40,h-40,'#0E1C29',42);s.rect(w/2-53,17,106,18,'#060D14',9)
 elif t=='dial':
  s.circle(w/2,h/2,195,color,C['edge'],5)
  for i in range(25):
   a=(150+i*240/24)*math.pi/180
   x1=w/2+159*math.cos(a);y1=h/2+159*math.sin(a);x2=w/2+181*math.cos(a);y2=h/2+181*math.sin(a)
   s.line(x1,y1,x2,y2,C['orange'] if i<15 else C['edge'],4)
 elif t=='marker':
  s.path('M95 10 C42 10 16 42 16 96 C16 151 95 219 95 219 C95 219 174 151 174 96 C174 42 148 10 95 10Z',color,C['white'],4)
  s.circle(95,94,37,'#172B37');s.icon('box' if 'dropoff' in name else 'warning' if 'warning' in name else 'pin',95,94,48,color)
 return s.close()

def svg_icon(name,col=C['orange']):
 s=S(128,128);s.raw(f'<g transform="translate(8 8) scale(4.65)" stroke="{col}" fill="none" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">{ICON_PATHS[name]}</g>')
 return s.close()

def rasterize(svg,png,scale=1):
 cairosvg.svg2png(bytestring=svg.encode('utf-8'),write_to=str(png),scale=scale)

def export_all():
 manifest=[]
 (SPRITE_ROOT/'Components').mkdir(parents=True,exist_ok=True)
 (SPRITE_ROOT/'Icons').mkdir(parents=True,exist_ok=True)
 for cat,key,title,code in SCREENS:
  d1=ROOT/'01_Screens/SVG'/cat;d2=ROOT/'01_Screens/PNG'/cat;d1.mkdir(exist_ok=True);d2.mkdir(exist_ok=True)
  svg=generate_screen(key).close()
  (d1/(key+'.svg')).write_text(svg,encoding='utf-8');rasterize(svg,d2/(key+'.png'))
  manifest.append(dict(category=cat,id=key,title_tr=title,source_scripts=code,svg=f'01_Screens/SVG/{cat}/{key}.svg',png=f'01_Screens/PNG/{cat}/{key}.png',resolution='1600x900',kind='UI concept mockup'))
  print('screen',len(manifest),key,flush=True)
 for name in COMPONENTS:
  svg=component_svg(name)
  (ROOT/'02_Components/SVG'/f'{name}.svg').write_text(svg,encoding='utf-8')
  rasterize(svg,SPRITE_ROOT/'Components'/f'{name}.png')
 for name in ICON_PATHS:
  svg=svg_icon(name)
  (ROOT/'03_Icons/SVG'/f'{name}.svg').write_text(svg,encoding='utf-8')
  rasterize(svg,SPRITE_ROOT/'Icons'/f'{name}.png')
 with (ROOT/'05_Docs/screen_manifest.csv').open('w',encoding='utf-8-sig',newline='') as f:
  ww=csv.DictWriter(f,fieldnames=list(manifest[0]));ww.writeheader();ww.writerows(manifest)
 (ROOT/'05_Docs/screen_manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
 return manifest

if __name__=='__main__':
 print('Starting export',flush=True)
 export_all(); print('Finished export',flush=True)

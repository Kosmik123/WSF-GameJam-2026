# WSF — assety biomów

Stylizowane, low-poly assety środowiska dla 6 biomów: **Las, Pustynia, Wybrzeże, Lód, Wulkan, Bagno**.
Każdy typ ma **4 warianty** (`_v0`–`_v3`). Razem **268 obiektów**, obecnie w pliku Blendera grafika (`Head.blend`, kolekcja `LD57_Biomes`).
Kolory są zapisane w **vertex colorach** — co trzeba przygotować w Unity opisuje sekcja [Vertex colory w Unity](#vertex-colory-w-unity--co-przygotować).

---

## Styl

- **Fasetowane bryły, kolor per ściana** — każda ściana ma jeden kolor (płaskie cieniowanie, *Shade Flat*).
- **Gradient wysokości** — podstawa ciemniejsza, końcówki jaśniejsze (paleta *ciemny → jasny*).
- **Rozrzut** — każda ściana ma losowo ±5–12% jasności (efekt „ręcznie malowanych” faset).
- **Kolor w atrybucie `Col`** (Color Attribute, *Face Corner*, Float Color) — bez tekstur.
- **Materiały** (wspólne dla wszystkich assetów):
  - `LD57 Stylized` — *Color Attribute (Col)* → Base Color, roughness 0,9.
  - `LD57 Stylized Glow` — *Color Attribute (Col)* → Emission (siła 2): lawa, żar, kryształy, świecące grzyby.
- **Pivot** każdego assetu na ziemi (z = 0), w środku podstawy.

## Struktura w Blenderze

```
LD57_Biomes
├── Biome_Las        ├── Las_Landmarki / Las_Duze / Las_Srednie / Las_Drobne
├── Biome_Pustynia   ├── Pustynia_Landmarki / …_Duze / …_Srednie / …_Drobne
├── Biome_Wybrzeze   ├── …
├── Biome_Lod        ├── …
├── Biome_Wulkan     ├── …
└── Biome_Bagno      └── …
```

- **Nazwy obiektów:** `<Biom>_<Typ>_v<wariant>`, np. `Pustynia_Kaktus_v2`, `Bagno_Lilie_wodne_v0`.
- **Położenie katalogu:** kolumny biomów obok siebie od **X = 40**, wiersze typów od **Y = −20** w dół, warianty wzdłuż X.

---

## 🌲 Las — 13 typów, 52 obiekty

**Paleta:** zielenie (`leaf`), jesienny pomarańcz / czerwień / żółć (`autumn`, `red`, `yellow`), kora (`bark`), jasne skały (`rock`) z mchem (`moss`).

| Kategoria | Typ | Opis |
|---|---|---|
| Landmarki | Wielkie drzewo | Duże drzewo liściaste z korzeniami; warianty: pomarańczowe, czerwone, żółte, zielone |
| Landmarki | Menhir | Wysoka bryła skalna + 2 mniejsze, mech na górze, kępa trawy u podstawy |
| Duże | Drzewo | v0 zielone liściaste, v1 jesienne, v2 sosna, v3 brzoza |
| Duże | Martwe drzewo | Pień z 4 gałęziami i odgałęzieniami, kolor suchego drewna |
| Średnie | Krzak | 3 kule liści; v2 jesienny, v2–v3 z kwiatami |
| Średnie | Głaz | Duży głaz + 2 mniejsze, mech na górnych ścianach |
| Średnie | Pniak | Ścięty pień ze słojami na górze i 5 korzeniami, opcjonalny mech |
| Średnie | Kłoda | Leżący pień, słoje na końcach, mech na górze (v3 bez mchu), sterczące gałęzie |
| Drobne | Trawa | v0–v1 zielona kępa, v2–v3 wysoka sucha trawa |
| Drobne | Paproć | Rozeta liści paproci; v3 żółknąca |
| Drobne | Kwiaty | Trawa + kwiaty: lawenda, czerwone, żółte, białe |
| Drobne | Grzyby | v0 muchomory, v1 brązowe, v2–v3 świecące (Glow) |
| Drobne | Kamyki | 3 płaskie kamyki |

## 🏜️ Pustynia — 12 typów, 48 obiektów

**Paleta:** piasek (`sand`), terakota (`terracotta`), ochra (`ochre`), szarozielony kaktus (`cactus`), suche zarośla (`drybrush`), kość (`bone`).
**Reguła specjalna:** skały w **warstwach** — kolor zależy od wysokości, pasy co 0,45 m (terakota / ochra / piasek).

| Kategoria | Typ | Opis |
|---|---|---|
| Landmarki | Łuk skalny | Łuk z 11 brył skalnych w warstwach, rozpiętość ok. 5–7 m |
| Landmarki | Wielki kaktus | Saguaro 6,5–8 m z 3–4 ramionami |
| Duże | Kaktus | Saguaro 2,8–4,2 m z 0–2 ramionami, żebrowanie (co druga kolumna ścian jaśniejsza) |
| Duże | Słup skalny | Wysoka kolumna w warstwach z szerszą „czapą” na górze |
| Duże | Palma | Lekko pochylony pień w pierścienie, 7–9 liści, kokosy |
| Średnie | Opuncja | Spłaszczone „tarcze” rosnące jedna z drugiej, różowe owoce |
| Średnie | Skała warstwowa | Szeroka bryła w warstwach |
| Średnie | Szkielet | Kręgosłup + łukowate żebra wychodzące z ziemi + czaszka z rogami (skala ok. 0,5) |
| Drobne | Kaktus kulisty | 1–3 kuliste kaktusy z żebrowaniem, różowe kwiaty |
| Drobne | Toczące zielsko | Kula z 16 splątanych łuków gałązek |
| Drobne | Sucha trawa | Kępa suchej trawy |
| Drobne | Kamyki | 3 kamyki w kolorze terakoty |

## 🏖️ Wybrzeże — 11 typów, 44 obiekty

**Paleta:** skały przybrzeżne (`coastrock`), wodorosty (`seaweed`), wybielone drewno (`driftwood`), muszle (`shell`), rozgwiazdy (`starfish`), korale (`coral_pink`, `coral_orange`, `coral_purple`, `coral_yellow`), drewno wraku (`wood`), palmy (`palm`, `palmbark`).

| Kategoria | Typ | Opis |
|---|---|---|
| Landmarki | Wrak | Kil, żebra kadłuba (część złamana), deski poszycia z jednej burty, złamany maszt; ok. 7 m |
| Landmarki | Łuk morski | Łuk skalny z wodorostami na dolnych, poziomych ścianach |
| Duże | Palma | Mocniej pochylona niż pustynna |
| Duże | Skała przybrzeżna | Duża skała + mniejsza, wodorosty na górze |
| Średnie | Wyrzucone drewno | Krzywa, wybielona kłoda bez mchu, 3 sterczące gałęzie |
| Średnie | Koral | Rozgałęziony koral na kamieniu; v0 różowy, v1 pomarańczowy, v2 fioletowy, v3 żółty |
| Drobne | Muszle | 2 spiralne muszle + rozgwiazda |
| Drobne | Rozgwiazda | Pojedyncza; v0 pomarańczowa, v1 różowa, v2 fioletowa, v3 żółta |
| Drobne | Wodorosty | Kępa szerokich, wysokich wodorostów |
| Drobne | Trawa wydmowa | Sztywna, cienka sucha trawa |
| Drobne | Otoczaki | 4 gładkie, płaskie kamienie |

## ❄️ Lód — 10 typów, 40 obiektów

**Paleta:** śnieg (`snow`), lód (`ice`), szaroniebieska skała (`icerock`), szron (`frost`).
**Reguła specjalna:** **śnieg** na ścianach skierowanych w górę (`snow_pass`); na sosnach tylko ok. 50% takich ścian (łaty śniegu).

| Kategoria | Typ | Opis |
|---|---|---|
| Landmarki | Lodowa iglica | Wielkie kryształy (×1,6) przemalowane na lód, skała u podstawy ze śniegiem |
| Landmarki | Zamarznięty szkielet | Szkielet olbrzyma (skala 1,5) ze śniegiem |
| Duże | Ośnieżona sosna | Sosna 5–7 m z łatami śniegu na igłach |
| Duże | Bryła lodu | Ostra bryła lodu + mniejsza, śnieg na górze |
| Średnie | Skała ze śniegiem | Szaroniebieska skała, śnieg na górnych ścianach |
| Średnie | Kryształy lodu | Kryształy przemalowane na lód (bez świecenia) |
| Średnie | Zaspa | Spłaszczona, półzakopana bryła śniegu |
| Drobne | Lodowe kolce | 5 lodowych kolców z ziemi |
| Drobne | Oszroniony krzak | Mały martwy krzak (skala 0,32), szron + śnieg |
| Drobne | Kamyki | 3 kamyki ze śniegiem |

## 🌋 Wulkan — 10 typów, 40 obiektów

**Paleta:** bazalt (`basalt`), popiół (`ash`), lawa (`lava`, Glow), węgiel drzewny (`charcoal`), obsydian (`obsidian`).
**Reguły specjalne:** **szczeliny lawy** — ok. 30% ścian skierowanych w bok / w dół dostaje kolor lawy i materiał Glow; **żar** — ok. 7% ścian spalonego drewna świeci.

| Kategoria | Typ | Opis |
|---|---|---|
| Landmarki | Krater | Pierścień 14 brył bazaltu wokół jeziora lawy, świecące szczeliny |
| Landmarki | Bazaltowa iglica | Pęk sześciokątnych kolumn bazaltu do 6–8 m |
| Duże | Kolumny bazaltowe | Pęk kolumn 2–3,2 m; jaśniejsze, płaskie wierzchołki |
| Duże | Skała z lawą | Bazaltowa skała + mniejsza, świecące szczeliny |
| Średnie | Spalone drzewo | Martwe drzewo w kolorze węgla z żarzącymi się ścianami |
| Średnie | Obsydian | Kryształy przemalowane na czarno-fioletowy obsydian, bazaltowa podstawa |
| Średnie | Komin | Stożek popiołu z otworem i świecącą lawą w środku, kamienie wokół |
| Drobne | Kałuża lawy | Świecący dysk (jaśniejszy w środku) z obwódką bazaltowych kamieni |
| Drobne | Żużel | Gruz w kolorze węgla z żarem |
| Drobne | Kamyki | 3 bazaltowe kamyki |

## 🐸 Bagno — 11 typów, 44 obiekty

**Paleta:** oliwkowe liście (`swampleaf`), ciemna kora (`swampbark`), wiszący mech (`spanishmoss`), mokry mech (`moss_wet`), turzyca (`sedge`), trzcina (`reed`), pałka (`cattail`), lilie (`lily`), fioletowa bioluminescencja (`glow_purple`).

| Kategoria | Typ | Opis |
|---|---|---|
| Landmarki | Stary cyprys | Gruby, krzywy pień 6–7,5 m, „kolana” cyprysa wokół, płaskie korony, wiszący mech |
| Landmarki | Powalony olbrzym | Kłoda 7–9 m z mokrym mechem i świecącymi fioletowymi grzybami |
| Duże | Drzewo na korzeniach | Pień uniesiony na 6–8 korzeniach-szczudłach, oliwkowa korona, wiszący mech |
| Duże | Martwe drzewo | Martwe drzewo w ciemnym, mokrym kolorze, mech na górnych ścianach |
| Średnie | Omszała kłoda | Kłoda z mokrym mechem |
| Średnie | Świecące grzyby | Grzyby ze świecącymi fioletowymi kapeluszami |
| Średnie | Pniak | Ciemny pniak z mchem |
| Drobne | Trzciny | Kępa trzcin + 3–6 pałek |
| Drobne | Lilie wodne | 4–7 liści z wycięciem, część z kwiatami (różowe / białe); leżą na z ≈ 0,02 |
| Drobne | Turzyca | Oliwkowa kępa trawy |
| Drobne | Paproć | Paproć z lasu |

---

## Wspólne reguły kolorowania

| Efekt | Reguła |
|---|---|
| Gradient | `lerp(ciemny, jasny, t)`, `t` = wysokość środka ściany w obiekcie (0…1) |
| Mech | ściany z `normal.z > 0,62` → paleta `moss` |
| Śnieg | ściany skierowane w górę → `snow` (dla igieł liczone `abs(normal.z)`, łaty 50%) |
| Warstwy skał | pasy wysokości co 0,45 m: terakota / ochra / piasek |
| Żebra kaktusa | co druga kolumna ścian (wg kierunku normalnej) jaśniejsza |
| Słoje | ściany na końcach kłód i na górze pniaków → jasne drewno |
| Lawa | ściany skierowane w bok/w dół (losowo) → `lava` + materiał Glow |
| Przemalowanie | `remap()` — nowa paleta wg jasności starego koloru (spalone drzewo, lód, obsydian, mokre drewno) |

## Skrypty

| Plik | Do czego |
|---|---|
| `ld57_biomes.py` | Generuje kolekcję `LD57_Biomes` (definicje biomów w słowniku `BIOMES`, nowe palety w `st.PAL.update(...)`) |
| `ld57_enviro_stylized.py` | Narzędzia bazowe (bryły, liście, drzewa, skały, trawa, materiały) i katalog `LD57_Enviro` |
| `ld57_enviro_import.py` | Elementy CaveDecor i materiał pajęczyny (wymagany przez powyższe) |

**Wszystkie trzy pliki muszą leżeć w jednym folderze.** Skrypty są u grafika (poza tym repozytorium).

- **Regeneracja:** uruchom `ld57_biomes.py` w Blenderze (Scripting → Run) albo:
  `blender -b Head.blend --python ld57_biomes.py` + zapis pliku.
  Skrypt usuwa i buduje od nowa całą kolekcję `LD57_Biomes` — **ręczne zmiany w tych obiektach zostaną nadpisane**.
- **Zmiana kolorów:** edytuj palety w `PAL` (sRGB, para *ciemny / jasny*) i uruchom ponownie.
- **Dodanie typu:** dopisz krotkę `("Kategoria", "Nazwa", builder)` do listy biomu w `BIOMES`.
- **Determinizm:** każdy wariant ma stałe ziarno (biom + indeks typu + wariant) — ponowne uruchomienie daje te same kształty.

---

## Vertex colory w Unity — co przygotować

Projekt: **URP 17.4, Linear color space**. Assety **nie mają tekstur** — cały kolor siedzi w wierzchołkach mesha. Standardowy shader **URP/Lit ignoruje vertex colory**, więc bez przygotowania wszystko wyjdzie szare/białe.

### 1. Jak kolor wędruje z Blendera do Unity

1. **Blender:** atrybut `Col` (Face Corner, Float Color). Face Corner = każdy narożnik ściany ma swój kolor, dzięki temu sąsiednie ściany mogą mieć różne kolory (płaskie, fasetowe plamy).
2. **Eksport FBX:** Blender zapisuje aktywny atrybut koloru jako warstwę *vertex color* w FBX (ustawienie *Geometry → Vertex Colors*, domyślnie **sRGB**).
3. **Import w Unity:** kolory lądują w `Mesh.colors` (dostępne w shaderze jako `COLOR` / węzeł *Vertex Color*). Tam, gdzie sąsiednie ściany mają różny kolor, Unity **rozdziela wierzchołki** — liczba wierzchołków w Unity będzie większa niż w Blenderze; to normalne i tanie przy low-poly.
4. **Shader** musi pobrać *Vertex Color* i użyć go jako Base Color (i Emission dla świecących).

> **Uwaga na przestrzeń kolorów:** FBX z ustawieniem *sRGB* zapisuje kolory w gammie, a projekt jest **Linear**. Unity nie konwertuje vertex colorów automatycznie, więc w shaderze trzeba je przeliczyć **sRGB → Linear** (węzeł *Colorspace Conversion*). Bez tego wszystko wyjdzie **wyblakłe/za jasne**.
> Alternatywa: eksport z *Vertex Colors: Linear* i brak konwersji w shaderze — ale wtedy ciemne kolory tracą precyzję (vertex colory są 8-bitowe), więc **zalecamy sRGB + konwersję w shaderze**.

### 2. Shadery (Shader Graph) — do przygotowania przez programistę

**A. `SG_VertexColorLit`** — dla slotu materiału `LD57 Stylized` (zdecydowana większość ścian)

- Graph: **URP Lit**, Surface: Opaque.
- `Vertex Color` → `Colorspace Conversion (RGB → Linear)` → **Base Color**.
- Smoothness ≈ **0,1** (w Blenderze roughness 0,9), Metallic **0**.
- Graph Settings → **Render Face: Both** (liście, igły, płatki, lilie to pojedyncze płaszczyzny — bez tego znikają od spodu).
- Opcjonalnie: parametr `Tint` (Color, domyślnie biały) mnożony z vertex colorem — łatwe przyciemnianie/rozjaśnianie całych biomów.

**B. `SG_VertexColorGlow`** — dla slotu `LD57 Stylized Glow` (lawa, żar, kryształy, świecące grzyby)

- Graph: **URP Unlit** (albo Lit z Emission).
- `Vertex Color` → `Colorspace Conversion (RGB → Linear)` → × `Intensity` (Float, domyślnie **2**, HDR) → **Base Color** (Unlit) / **Emission** (Lit).
- Żeby faktycznie „świeciło”: w URP Asset włączone **HDR**, w scenie Global Volume z **Bloom** (Threshold ok. 1, Intensity 0,5–1).

**C. Pajęczyny (`Decor Web`)** — jedyny materiał z teksturą: osobny **URP Unlit, Transparent, Render Face: Both**, tekstura pajęczyny z kanałem alfa (wyeksportować z Blendera jako PNG).

**Szybka alternatywa bez Shader Grapha** (na prototyp): shader **Universal Render Pipeline/Particles/Lit** z *Color Mode: Multiply* używa vertex colorów. Działa od ręki, ale nie robi konwersji sRGB→Linear (kolory będą jaśniejsze) — tylko na test.

### 3. Ustawienia importu FBX (Model Importer)

| Zakładka | Ustawienie | Wartość |
|---|---|---|
| Model | Scale Factor / Convert Units | tak, żeby 1 m w Blenderze = 1 jednostka Unity |
| Model | Bake Axis Conversion | **włączone** (Blender: Z w górę → Unity: Y w górę) |
| Model | Normals | **Import** (assety mają płaskie cieniowanie — nie przeliczać gładko) |
| Model | Read/Write | wyłączone (chyba że kod modyfikuje meshe) |
| Materials | Material Creation Mode | Import via MaterialDescription |
| Materials | Remapped Materials | `LD57 Stylized` → materiał z `SG_VertexColorLit`, `LD57 Stylized Glow` → materiał z `SG_VertexColorGlow` |

Eksport z Blendera (FBX): *Apply Scalings: FBX All*, *Forward: -Z*, *Up: Y*, *Apply Transform*, *Geometry → Vertex Colors: sRGB*, eksportować tylko zaznaczone obiekty (*Selected Objects*).

### 4. Wydajność i układ w scenie

- Wszystkie assety dzielą **2 materiały** (+ pajęczyna) → świetnie się batchują (SRP Batcher). Statyczne propy oznaczyć jako **Static** (static batching) albo instancjonować przez GPU Instancing.
- **Pivot** każdego assetu jest na ziemi w środku podstawy → przy rozmieszczaniu wystarczy pozycja na powierzchni + obrót wg normalnej terenu.
- Kanał **alfa vertex coloru jest wolny** (zawsze 1) — można go później użyć np. jako maski wiatru dla liści i trawy.

### 5. Checklista dla programisty

- [ ] `SG_VertexColorLit` (Lit, Vertex Color → sRGB→Linear → Base Color, Render Face: Both)
- [ ] `SG_VertexColorGlow` (Unlit/Emission, Vertex Color → sRGB→Linear → × Intensity)
- [ ] 2 materiały na tych shaderach, nazwane jak sloty z Blendera (`LD57 Stylized`, `LD57 Stylized Glow`) — wtedy remap materiałów zadziała po nazwie
- [ ] HDR w URP Asset + Global Volume z Bloom
- [ ] Preset Model Importera (tabela wyżej) dla folderu z assetami biomów
- [ ] Materiał pajęczyny (Unlit, Transparent, Both) + tekstura

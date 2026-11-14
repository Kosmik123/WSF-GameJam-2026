# PSG: Path Scatter Generator (addon Blendera)

Dwa generatory: **obszar** (scatter na zaznaczonej płaszczyźnie, drzewa w klastrach, trawa i krzaki wokół nich) oraz **ścieżka**. Przy ścieżce rysujesz linię adnotacją, addon zamienia ją w edytowalną krzywą, rozmieszcza wzdłuż niej assety (np. z biomów z [WSF_Biom.md](WSF_Biom.md)) i maluje pod nią podłoże vertex colorem z miękkim przejściem na krawędziach. Wynik eksportuje się do Unity 6 jako FBX ze współdzielonymi meshami.

Kod: [Tools/Blender/psg_scatter/](../Tools/Blender/psg_scatter/). Testowane w Blenderze 5.3 (minimum 4.2).

## Instalacja

1. Spakuj folder `Tools/Blender/psg_scatter` do zipa (zip ma zawierać folder `psg_scatter` z `__init__.py` i `blender_manifest.toml`).
2. Blender → Edit → Preferences → Get Extensions → menu ▾ → **Install from Disk** → wskaż zip.
3. Panel: widok 3D → pasek boczny (`N`) → zakładka **PSG**.

## Dlaczego instancjonowanie (zgodność z Unity)

Każdy wygenerowany element to **zwykły obiekt** (linked duplicate), który **współdzieli mesh** z obiektem źródłowym. Nie ma modyfikatorów Geometry Nodes ani instancji kolekcji, których FBX nie przenosi.

- W Blenderze każdy element da się zaznaczyć, przesunąć, obrócić, usunąć albo podmienić.
- Eksport FBX zapisuje jeden mesh na wariant. Unity importuje go jako jeden asset `Mesh`, do którego odwołują się wszystkie obiekty, więc działają SRP Batcher i GPU Instancing (włącz *Enable GPU Instancing* w materiale).
- Sprawdzone: 73 instancje + podłoże po eksporcie i ponownym imporcie dają 4 unikalne meshe (3 warianty + podłoże).

## Przepływ pracy

1. **Default Source**: kolekcja z wariantami (np. `Las_Drobne`). **Default Ground**: płaszczyzna terenu (opcjonalnie).
2. **Draw Path** włącza narzędzie Annotate z umieszczaniem na powierzchni. Narysuj linie (każda kreska to osobna ścieżka).
3. **Annotation to Path**: kreski zamieniają się w krzywe w kolekcji `PSG_Paths` (upraszczanie RDP, opcjonalnie Bezier). Kreski adnotacji są usuwane (opcja *Remove Strokes*). Gotową krzywą można też podpiąć przyciskiem **Use Curve as Path**.
4. Brak terenu? **Create Ground Plane** tworzy siatkę pod ścieżką (rozmiar oczka: *Ground Cell*).
5. Edytuj krzywą jak każdą inną (Tab, przesuwanie punktów). Przy włączonym **Live Update** instancje i malowanie odświeżają się same.

Instancje trafiają do `PSG_Generated/PSG_<ścieżka>`, są osadzone na terenie (raycast w dół) i mają pivot na ziemi.

## Scatter na płaszczyźnie (obszar)

1. Zaznacz płaszczyznę albo teren i kliknij **Scatter on Selected Mesh**. W oknie wybierz biom i czy dodać preset.
2. Preset biomu dodaje 5 warstw. Kolekcje bierze z `all_models.blend` (`<Biom>_Duze`, `_Srednie`, `_Drobne`):

| Warstwa | Tryb | Co robi |
|---|---|---|
| Drzewa | Clusters | grupki 2–4 drzew w promieniu 4 m, 35% szans na pojedyncze drzewo |
| Krzewy | Around → Drzewa | 0–2 krzaki, głazy, pniaki wokół każdego drzewa |
| Trawa wokol | Around → Drzewa | 6–14 kęp trawy, paproci, kwiatów, grzybów wokół każdego drzewa |
| Trawa | Scatter | rzadka trawa na całym obszarze, z dala od innych warstw |
| Kamienie | Scatter | pojedyncze kamyki |

3. Warstwy dodajesz, kopiujesz i ustawiasz tak samo jak na ścieżce. **Apply Biome Preset** podmienia warstwy obszaru na preset innego biomu.

Tryby obszaru:

- **Scatter**: równomiernie po powierzchni, *Density* = sztuk na m².
- **Clusters**: *Clusters / 100 m²*, *Cluster Radius*, *Min/Max Count*, *Single Chance* (szansa na pojedynczy egzemplarz).
- **Around** (także na ścieżkach): wokół każdego elementu warstwy *Parent*. Ustawiasz promień min/max, liczbę min/max i *Chance*. *Scale with Parent* mnoży promień przez skalę rodzica. Warstwa rodzica musi być wyżej na liście. Usunięcie drzewa usuwa przy regeneracji jego trawę i krzaki (chyba że są zablokowane).

Wspólne dla wszystkich trybów:

- **Filter**: które obiekty z kolekcji brać, po fragmentach nazw oddzielonych przecinkami, `!` wyklucza. Na przykład `Trawa, Kwiaty` albo `!Kamyki, !Grzyby`. Pod polem widać liczbę pasujących wariantów.
- **Avoid Other Layers**: odległość od elementów warstw wyżej na liście (oprócz *Parent*), np. żeby kamienie nie wchodziły w drzewa.

Elementy osadzają się na powierzchni obszaru (raycast z góry), więc teren może mieć pagórki. Obszar może też być terenem (*Ground*) dla ścieżek, a ścieżki malują na nim drogi.

## Warstwy scatteru (kilka na ścieżkę)

Każda ścieżka ma listę warstw, np. drzewa na brzegach + trawa w pasie + kamienie na środku. Każda warstwa ma własną kolekcję źródłową i parametry:

| Parametr | Znaczenie |
|---|---|
| Mode | **Along**: co *Spacing* wzdłuż linii. **Edges**: pary po obu stronach w odległości *Width* (płoty, obrzeża). **Band**: losowe wypełnienie pasa według *Density* (szt./m²) |
| Width / Inner Width | zewnętrzna i wewnętrzna połowa szerokości pasa; *Inner Width* zostawia środek pusty (droga, rzeka) |
| Edge Falloff | przerzedzenie ku zewnętrznej krawędzi |
| Min Distance | minimalny odstęp między instancjami warstwy |
| Scale Min/Max, Random Yaw, Yaw Offset | losowa skala i obrót |
| Align to Path / Align to Normal | obrót zgodnie z kierunkiem ścieżki, pochylenie do normalnej terenu |
| Max Slope, Z Offset, Skip Off-Ground | odrzucanie stromych miejsc, przesunięcie w pionie, pomijanie punktów poza terenem |
| Seed | ziarno; ten sam seed daje ten sam układ |

Waga wariantu: własność `psg_weight` (Custom Property) na obiekcie źródłowym, domyślnie 1.

## Kontrola nad wygenerowanymi elementami

- **Keep Edited** (domyślnie włączone): element, który ręcznie przesuniesz, obrócisz, przeskalujesz albo któremu zmienisz mesh, zostaje automatycznie **zablokowany** i przetrwa regenerację.
- **Keep Deletions**: usunięty element nie wraca po regeneracji.
- Panel **Selected Instances**: Lock/Unlock, **Random Variant**, **Next Variant** (przełącza `_v0` → `_v1` → …), **Reroll Rotation/Scale**, **Snap to Ground**, **Detach** (element przestaje być zarządzany), **Select Path**.
- Na ścieżce: **Regenerate**, **Clear**, **Reset Edits** (zapomina usunięcia i blokady), **Make Permanent** (odpina wszystkie elementy od generatora), **Select All / Select Locked**.
- Zmiana parametrów warstwy (spacing, seed, tryb) zmienia numerację kandydatów, więc zapamiętane usunięcia mogą trafić w inne miejsca. Zablokowane elementy zostają tam, gdzie są.

## Wydajność

- Regeneracja aktualizuje istniejące obiekty w miejscu (pozycja, mesh) zamiast usuwać je i tworzyć od nowa, a niepotrzebne usuwa hurtowo. Pomiar na 1300 obiektach w `all_models.blend`: ok. 0,4 s zamiast 4 s.
- **Display As** w każdej warstwie: Textured, Solid, Wire albo **Bounds** (sam prostopadłościan, najszybciej). Działa od razu, bez regeneracji, i jest zachowywane przy kolejnych regeneracjach. Przyciski w panelu Generator ustawiają tryb dla wszystkich warstw naraz. Na eksport nie wpływa.
- Live Update czeka 0,35 s ciszy po ostatniej zmianie (np. po puszczeniu suwaka), zanim przeliczy scatter.
- Na laptopie z dwiema kartami przypnij Blendera do karty NVIDIA: Ustawienia Windows → System → Ekran → Grafika → `blender.exe` → Wysoka wydajność.
- Kolekcje, nad którymi nie pracujesz, wykluczaj z widoku checkboxem w Outlinerze. Samo ukrycie oczkiem nie zdejmuje obiektów z obliczeń.

## Malowanie terenu (kolory jak w assetach)

Panel **Terrain Paint** działa na zaznaczonym meshu (obszarze, terenie albo *Ground* ścieżki). Przycisk **Paint / Sample Biome Palette** włącza cieniowanie i pobiera paletę z assetów wybranego biomu. **Scatter on Selected Mesh** robi to od razu (opcja *Paint Terrain*). Kolor trafia do `Col` (Face Corner), tak jak w assetach, a teren dostaje materiał `LD57 Stylized`.

Zasady, te same co w assetach z [WSF_Biom.md](WSF_Biom.md#wspólne-reguły-kolorowania):

- **Gradient wzdłuż Z**: najniższy punkt dostaje kolor ciemny, najwyższy jasny (`lerp(ciemny, jasny, t)`). *Z Gradient* ustawia siłę. *Auto Range* bierze zakres z wysokości meshu, bez niego zakres wpisujesz w *Z Min/Max*.
- **Nachylenie**: ściany bardziej strome niż *Slope Angle* dostają paletę skały. *Slope Blend* ustawia płynne przejście.
- **Noise**: *Noise Brightness* przesuwa kolor po palecie ciemny/jasny. *Noise Bias* tworzy większe plamy przechylone ku kolorowi *Bias*, np. sucha trawa w lesie.
- **Fasety**: *Faceted* daje jeden kolor na ścianę, a *Facet Jitter* losową jasność ±% na ścianę (w assetach 5–12%).
- Paleta pochodzi z kolorów assetów biomu (percentyle jasności): Las to trawa + kamyki + sucha trawa, Pustynia to piasek ze skał warstwowych + terakota, Lód to zaspa + skała ze śniegiem i tak dalej (`TERRAIN_SOURCES` w `core.py`). Każdy kolor można potem zmienić ręcznie.
- Drogi ze ścieżek malują się na wierzchu, a *Facet Jitter* obejmuje też je.
- Preset biomu nie stawia drzew i trawy na stromych ścianach (*Max Slope* 30–35°).

## Malowanie podłoża

- Kolor trafia do atrybutu `Col` (Face Corner, Float Color), tak samo jak w assetach biomów. Teren dostaje materiał `LD57 Stylized` (Color Attribute → Base Color), jeśli w pliku nie ma żadnego.
- Każda ścieżka ma: **Color**, **Width** (pełne krycie), **Blend** (płynne przejście smoothstep), **Strength**, **Edge Noise** + **Noise Scale** (postrzępiona krawędź), **Order** (wyższy maluje na wierzchu).
- Teren ma **Base Color**. Malowanie jest nieniszczące: kolor liczony jest od nowa z koloru bazowego i wszystkich ścieżek, które na nim malują.
- **Mask in Alpha**: zapisuje łączne pokrycie ścieżek w kanale alfa (np. do maski w shaderze terenu). Domyślnie alfa = 1.

## Eksport do Unity

Przycisk **Export FBX for Unity** eksportuje instancje i tereny ścieżek z ustawieniami z [WSF_Biom.md](WSF_Biom.md#3-ustawienia-importu-fbx-model-importer): *Apply Scalings: FBX All*, *Forward: -Z*, *Up: Y*, *Apply Transform*, vertex colory sRGB, tylko meshe.

W Unity: Model Importer jak dla assetów biomów (Bake Axis Conversion, Normals: Import), materiał terenu na `SG_VertexColorLit` (konwersja sRGB → Linear w shaderze).

## Ograniczenia

- Malowanie liczy odległość w płaszczyźnie XY, więc nadaje się do terenów „z góry”. Teren w kształcie torusa trzeba malować na płaskim kawałku przed wygięciem albo dopisać projekcję.
- Modyfikatory obiektu źródłowego nie przechodzą na instancje (współdzielony jest tylko mesh). Zastosuj je w źródle.
- Kreski adnotacji w trybie 2D (widok, obraz) są pomijane. Rysuj z umieszczaniem *Surface* albo *3D Cursor*.
- Eksporter FBX wypisuje w konsoli ostrzeżenia „Cannot register a valid material index” przy współdzielonych meshach. Sprawdziłem ponowny import: materiały, indeksy ścian i współdzielenie meshy się zgadzają, więc ostrzeżenia nie mają skutków.
- Obszar w trybie edycji (Tab) nie odświeża się na bieżąco. Scatter przelicza się po wyjściu z edycji.

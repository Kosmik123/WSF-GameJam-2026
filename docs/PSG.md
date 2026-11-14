# PSG: Path Scatter Generator (addon Blendera)

Rysujesz linię adnotacją, addon zamienia ją w edytowalną krzywą, rozmieszcza wzdłuż niej assety (np. z biomów z [WSF_Biom.md](WSF_Biom.md)) i maluje pod nią podłoże vertex colorem z miękkim przejściem na krawędziach. Wynik eksportuje się do Unity 6 jako FBX ze współdzielonymi meshami.

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

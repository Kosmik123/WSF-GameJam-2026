# GDD: Uroboros (WSF GameJam 2026)

> Wersja robocza, piszemy razem. Rzeczy oznaczone **[DO USTALENIA]** są otwarte.
> Lista zadań: [TASKS.md](TASKS.md).

## 1. Wizja w jednym zdaniu

Uciekasz po ciele węża zwiniętego w pierścień przed jego własną paszczą, która go pożera. Musisz okrążyć cały świat i stanąć za paszczą, na czubku głowy.

## 2. Temat i odniesienie do tematu (Uroboros)

- Świat to wąż pożerający własny ogon: torus, na którego powierzchni toczy się gra.
- Cykl jest też mechaniką: po zwycięstwie zaczyna się kolejna runda, w której paszcza jest szybsza. Koniec jest początkiem.
- Cel ucieczki (za paszczą) jest tam, gdzie zaczęliśmy: pętla zamyka się w miejscu startu.

## 3. Filary projektu

1. **Prezentacja przede wszystkim.** Jam jest oceniany głównie na podstawie prezentacji, więc gra ma wyglądać i brzmieć mocno nawet kosztem głębi systemów.
2. **Presja ucieczki.** Paszcza jest zawsze za plecami. Każda decyzja (walczyć czy biec) to wymiana czasu na bezpieczeństwo.
3. **Zręcznościowa walka.** Umiejętność gracza (unik, timing) ma znaczenie, a nie tylko statystyki.
4. **Jedna czytelna pętla.** Uciekaj, walcz, zdobywaj poziomy i sprzęt, okrąż świat, powtórz szybciej.
5. **Walka się opłaca.** Samo bieganie nie wystarcza: trudność rośnie, więc trzeba levelować wcześniej, żeby przetrwać później.

## 4. Świat

- **Kształt:** torus 3D. Pozycje w świecie opisują dwa kąty: `θ` (wokół dużego koła, kierunek biegu) i `φ` (wokół przekroju rury). Gracz może wejść na spód i do środka dziury.
- **Segmenty:** kilkanaście (domyślnie 14) proceduralnych segmentów ciała węża. Każdy ma własną rozdzielczość siatki i własny profil terenu, a granice między segmentami płynnie się łączą.
- **Zróżnicowanie:** każdy segment może mieć inny charakter terenu (płaski, pagórki, kratery, grzbiety) i inny materiał.
- **Wygląd:** stylizowany low poly, ciało węża (łuski, wzór). **[DO USTALENIA]** paleta kolorów i referencje.

## 5. Zagrożenie: paszcza

- Strefa śmierci w kształcie szczęki węża, która krąży po torusie.
- Porusza się **jednostajnie**, ze stałą prędkością kątową. Nie przyspiesza w ramach rundy.
- Dotknięcie paszczy zabija natychmiast, niezależnie od HP i nietykalności (unik, chwila po trafieniu). Paszcza nie zadaje obrażeń, tylko woła `Health.Kill()`, a śmierć od paszczy i od HP = 0 idzie tą samą ścieżką (`OnDied`). Wykrywanie liczymy z kątów `θ` gracza i głowy, bez fizyki i colliderów. Strefa ma nieregularny kształt zależny od `φ`: górna i dolna szczęka sięgają dalej do przodu, policzki są cofnięte do tyłu (dwie krzywe w Inspektorze: zasięg do przodu i do tyłu).
- Gracz musi być szybszy od paszczy, żeby ją okrążyć, więc walka z wrogami kosztuje czas.
- Wygląd: animowana szczęka (otwiera się i zamyka), po bokach strefy zagrożenia efekty (poświata, dźwięk, drgania kamery).

## 6. Pętla rozgrywki

1. Start: gracz stoi z przodu paszczy.
2. Gracz biegnie wzdłuż torusa, przed nim pojawiają się wrogowie.
3. Gracz walczy lub ich omija. Straty HP i czas spędzony w walce zmniejszają przewagę nad paszczą.
4. Gracz okrąża cały torus i dociera na czubek głowy węża (za paszczą).
5. **Zwycięstwo rundy.** Zaczyna się kolejna runda: paszcza jest szybsza, wrogów jest więcej.
6. Śmierć (paszcza albo HP = 0) **kończy całe przejście**: kasuje rundę i cały postęp (poziom, sprzęt). Gracz zaczyna od rundy 1, a ekran śmierci pokazuje, ile rund przetrwał.

**Runda** to jedno pełne okrążenie: od startu przed paszczą do dotarcia na czubek głowy. Po każdej wygranej rundzie kolejna ma szybszą paszczę i więcej wrogów. Rund jest tyle, ile gracz wytrzyma. **Rekord gracza to liczba ukończonych rund**: im więcej, tym lepszy wynik. Rekord zapisujemy i pokazujemy na ekranie startowym i śmierci.

## 7. Gracz

- Kamera: z góry pod skosem, pomiędzy top-down a widokiem zza pleców. „Góra” kamery to normalna powierzchni, więc świat obraca się wokół gracza.
- Ruch: po powierzchni torusa (współrzędne `θ`, `φ`), z kontrolerem kinematycznym, bez fizyki na zakrzywionej powierzchni.
- Zdrowie: pasek HP, wrogowie zadają obrażenia. **[DO USTALENIA]** czy HP się regeneruje albo leczy.
- Walka: **melee + unik** (hack and slash). Atak bronią, unik (przewrót lub dash z krótką nietykalnością), ewentualnie parowanie. **[DO USTALENIA]** czy jest jedna broń.
- Prototyp (etap 2): cios w łuku przed graczem, combo 3 ciosów (ostatni mocniejszy, większy odrzut), unik do przodu lub do tyłu (zależnie od wciśniętego kierunku) z nietykalnością, unik przerywa końcówkę ciosu. Po trafieniu gracz ma chwilę nietykalności. Trafienia liczymy po odległości na powierzchni torusa, nie przez collidery.

## 8. Wrogowie

- Zakres: **4 lub więcej typów** (ambitnie, patrz ryzyka).
- Wrogowie pojawiają się w współrzędnych `(θ, φ)` na torusie, przed graczem.
- **Typy:** biegacz (szybki, słaby), strzelec (atak z dystansu), pancerny (wolny, wytrzymały), rój (wiele małych). Mini-boss na czubku głowy to opcja, jeśli zostanie czas.
- **[DO USTALENIA]** dokładne statystyki (ustalimy na prototypie).
- Zachowania z prototypu (na bazie LD57): **biegacz** otacza gracza razem z innymi biegaczami, miga przed atakiem, wyskakuje do przodu i odskakuje. **Strzelec** trzyma dystans, chodzi bokiem, ucieka, gdy gracz jest blisko, i strzela po zapowiedzi (błysk i linia celowania). Trafienie w trakcie zapowiedzi przerywa atak wroga.
- Trudność wrogów zależy od **odległości od paszczy** (patrz sekcja 9), a z kolejnymi rundami rośnie dodatkowo mnożnik: więcej wrogów i szybsza paszcza.

## 9. Progresja

Progresja ma zmusić gracza do walki. Bez niej opłacałoby się tylko biec, więc **trudność rośnie szybciej, niż da się ją przeskoczyć samym biegiem**: gracz musi walczyć wcześnie (na łatwiejszych odcinkach), żeby zdobyć poziomy i sprzęt, a potem poradzić sobie z trudniejszymi.

- **Poziomy gracza:** wrogowie dają doświadczenie (XP), po zebraniu progu gracz awansuje. Każdy poziom zwiększa statystyki (HP, obrażenia, szybkość ataku).
- **Ekwipunek:** wrogowie (i skrzynie) zostawiają przedmioty: broń, pancerz, dodatki. Ekwipunek zmienia statystyki i może dawać prostą zdolność. **[DO USTALENIA]** liczba slotów i typów przedmiotów.
- **Trudność zależy od bliskości paszczy.** Liczymy ją dla każdego segmentu z jego odległości od paszczy, mierzonej w kierunku biegu gracza:
  - blisko paszczy: wrogowie najsłabsi (dobre miejsce na zdobywanie XP),
  - dalej: coraz silniejsi,
  - tuż za paszczą, przy czubku głowy: najsilniejsi (cel okrążenia jest najtrudniejszy).
- **Paszcza osłabia segmenty, które pożera.** Gdy paszcza zbliża się do kolejnych segmentów, trudność każdego z nich spada, bo dzieli go od niej coraz mniej. Jest to też nagroda za wolne tempo: zostawanie z tyłu oznacza łatwiejszych wrogów, ale mniej czasu.
- **Kolejne rundy:** do trudności z odległości dochodzi mnożnik rundy (liczniejsi i silniejsi wrogowie, szybsza paszcza).
- **Wróg dostaje siłę raz, w momencie pojawienia się**, na podstawie trudności segmentu, w którym powstaje. Później jej nie zmienia, więc gracz może ocenić ryzyko walki. Wrogów pojawiamy blisko gracza (kilka sekund przed nim), żeby ich poziom odpowiadał miejscu, w którym naprawdę walczy.
- **Dylemat:** walka daje siłę, ale kosztuje czas, a paszcza goni. Pominięcie walk na początku oznacza słabszego gracza później.
- **Kolejne rundy:** poziom i ekwipunek **zostają** po wygranej rundzie. Trudność kolejnej rundy rośnie szybciej niż samo wzmocnienie gracza.
- **Po śmierci:** koniec przejścia, poziom i sprzęt przepadają, nowa gra zaczyna się od rundy 1 (patrz sekcja 6). To klasyczny roguelite: każda próba zaczyna od zera.
- Rozbudowana meta-progresja (waluta, drzewko ulepszeń poza rundą) jest poza zakresem.

## 10. Czytelność i interfejs

- Wskaźniki na ekranie: **kierunek i odległość do paszczy** oraz **pasek postępu okrążenia** (gdzie jest cel).
- Efekty w świecie: poświata, dźwięk i drgania kamery przy zbliżaniu się paszczy.
- UI: HP gracza, numer rundy, ekran startowy, ekran zwycięstwa rundy, ekran śmierci.

## 11. Styl wizualny i dźwięk

- **Grafika:** stylizowany low poly, spójna paleta, shadery i efekty cząsteczkowe podbijające prezentację (łuski, mgła, poświata paszczy). **[DO USTALENIA]** paleta i referencje.
- **Dźwięk:** muzyka rosnąca w napięciu przy zbliżaniu się paszczy, efekty walki, dźwięk paszczy. Źródło: mieszanka darmowych paczek (CC0), pracy ręcznej i syntezatorów. Przy każdym assecie z zewnątrz sprawdzamy licencję.

## 12. Zakres i ryzyka

| Ryzyko | Jak ograniczamy |
|--------|-----------------|
| Za dużo typów wrogów (4+) na kilkadziesiąt godzin | Najpierw 2 typy z działającą walką, kolejne dokładamy według priorytetów w [TASKS.md](TASKS.md). |
| Kamera i sterowanie na torusie wymagają dopracowania | Prototyp ruchu i kamery jako pierwsze zadanie po świecie. |
| Walka zręcznościowa wymaga czasu na „feel” | Zrobić wcześnie, grywalny prototyp walki z jednym wrogiem. |
| Jam jest oceniany po prezentacji | Zarezerwować ostatnie godziny na polish, efekty, dźwięk i nagranie prezentacji. |

## 13. Zespół

- Programista (gameplay i systemy).
- Grafik (świat, postać, wrogowie, paszcza, UI, efekty).
- Claude (kod: świat, ruch, kamera, walka; według podziału w [TASKS.md](TASKS.md)).

## 14. Otwarte pytania

- Dokładne zachowania i statystyki wrogów.
- Progresja: krzywa XP, co daje poziom, liczba slotów i typów przedmiotów, źródła przedmiotów.
- Paleta kolorów i referencje wizualne.

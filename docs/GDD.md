# GDD: Uroboros (WSF GameJam 2026)

> Wersja robocza, piszemy razem. Rzeczy oznaczone **[DO USTALENIA]** są otwarte.
> Lista zadań: [TASKS.md](TASKS.md).

## 1. Wizja w jednym zdaniu

Uciekasz po ciele węża zwiniętego w pierścień przed jego własną paszczą, która go pożera. Musisz okrążyć cały świat, dotrzeć za paszczę i zniszczyć wrażliwy punkt węża.

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
- Gracz przed nią **ucieka**. Jej prędkość zależy od odległości gracza (w θ, przed głową): im bliżej gracz (im łatwiej go pożreć), tym wolniejsza, do progu minimalnej prędkości (`angularMoveSpeedDegrees` w odległości do `closeThetaDegrees`). Dalej przyspiesza liniowo z odległością (`speedGainPerDegree`), bez górnej granicy, więc nie da się jej zgubić samym biegiem. Mnożnik rundy dalej przyspiesza ją z rundy na rundę.
- Przebywanie w strefie paszczy **stopniowo odbiera życie graczowi** (obrażenia na sekundę, `damagePerSecond`), a NPC **zabija od razu**. Tryb wybiera się w komponencie `Health` (pole `Maw Damage Mode`: Instant lub Gradual). Obrażenia ignorują nietykalność (unik, chwila po trafieniu), więc unik nie ratuje przed paszczą. Gdy HP gracza spadnie do 0, ginie tą samą ścieżką co od wrogów (`OnDied`). Wykrywanie liczymy z kątów `θ` gracza i głowy, bez fizyki i colliderów. Strefa ma nieregularny kształt zależny od `φ`: górna i dolna szczęka sięgają dalej do przodu, policzki są cofnięte do tyłu (dwie krzywe w Inspektorze: zasięg do przodu i do tyłu). Strefę można też przesunąć wzdłuż ciała: o ile obiekt strefy jest przesunięty względem głowy w jej osi „do przodu”, o tyle przesuwa się cała strefa. Kształt obraca się wokół ciała razem z rotacją obiektu strefy względem głowy (obrót wokół jego osi „do przodu”), więc obracając strefę zmieniamy, gdzie są szczęki, a gdzie policzki.
- Gracz ucieka przed paszczą, a walka z wrogami kosztuje czas, który zmniejsza dystans do niej.
- Wygląd: animowana szczęka (otwiera się i zamyka), po bokach strefy zagrożenia efekty (poświata, dźwięk, drgania kamery).

## 6. Pętla rozgrywki

1. Start: gracz stoi z przodu paszczy.
2. Gracz biegnie wzdłuż torusa, przed nim pojawiają się wrogowie.
3. Gracz walczy lub ich omija. Straty HP i czas spędzony w walce zmniejszają przewagę nad paszczą.
4. Gracz okrąża cały torus, dociera do wrażliwego punktu węża (za paszczą) i niszczy go: punkt ma życie, maleje wraz z jego utratą i miga na czerwono po trafieniu.
5. **Zwycięstwo rundy.** Zaczyna się kolejna runda: paszcza jest szybsza, wrogów jest więcej.
6. Śmierć (paszcza albo HP = 0) **kończy całe przejście**: kasuje rundę i cały postęp (poziom, sprzęt). Gracz zaczyna od rundy 1, a ekran śmierci pokazuje, ile rund przetrwał.

**Runda** to jedno pełne okrążenie: od startu przed paszczą do zniszczenia wrażliwego punktu za głową. Po każdej wygranej rundzie kolejna ma szybszą paszczę i więcej wrogów. Rund jest tyle, ile gracz wytrzyma. **Rekord gracza to liczba ukończonych rund**: im więcej, tym lepszy wynik. Rekord zapisujemy i pokazujemy na ekranie startowym i śmierci.

## 7. Gracz

- Kamera: z góry pod skosem, pomiędzy top-down a widokiem zza pleców. „Góra” kamery to normalna powierzchni, więc świat obraca się wokół gracza.
- Ruch: po powierzchni torusa (współrzędne `θ`, `φ`), z kontrolerem kinematycznym, bez fizyki na zakrzywionej powierzchni.
- Zdrowie: pasek HP, wrogowie zadają obrażenia. **[DO USTALENIA]** czy HP się regeneruje albo leczy.
- Walka: **melee + unik** (hack and slash). Atak bronią, unik (przewrót lub dash z krótką nietykalnością), ewentualnie parowanie. **[DO USTALENIA]** czy jest jedna broń.
- Prototyp (etap 2): cios w łuku przed graczem, combo 3 ciosów (ostatni mocniejszy, większy odrzut), unik do przodu lub do tyłu (zależnie od wciśniętego kierunku) z nietykalnością, unik przerywa końcówkę ciosu. Po trafieniu gracz ma chwilę nietykalności. Trafienia liczymy po odległości na powierzchni torusa, nie przez collidery.
- **Klasy postaci** (z LD57): **Wojownik** (melee i unik, jak wyżej; postać z mieczem i tarczą) i **Mag** (strzela pociskami z dystansu, przytrzymanie ataku strzela seriami; unik zostaje; postać bez broni w niebieskim stroju). Model: Kapłan Pierzastego Węża (sługa węża-stwórcy Q'uq'umatza, przeciwnika węża-niszczyciela), low poly w zieleni, złocie i czerwieni, na szkielecie i z animacjami RPG Tiny Hero Duo z LD57. Mag ma długi pióropusz, pelerynę i laskę z głową węża, Wojownik krótszy pióropusz, osłony policzków, włócznię i tarczę z piór. Klasa to asset `PlayerClass` (atak, HP, pula skilli, skille startowe), wybierana na graczu w `PlayerClassApplier`. **[DO USTALENIA]** czy gracz wybiera klasę na ekranie startowym i czy Wojownik też dostaje skille (w LD57 miał wir, szarżę, uderzenie w ziemię, rzut toporem, okrzyk).
- **Skille** (z LD57): do 4 slotów (Q, E, R, F; na padzie LB, RB, LT, RT), każdy skill ma cooldown i 4 poziomy: baza, dwa ulepszenia i ewolucja, która zmienia działanie. Skille trafiają tak samo jak walka wręcz (odległość na torusie, nietykalność, drużyny). Mag zaczyna z kulą ognia, lodową novą, łańcuchem błyskawic i teleportem; magiczne kule są w puli na później. Kolejne skille i poziomy mają dawać awanse (karty przy level upie, patrz sekcja 9). Śmierć resetuje skille do startowych.
  - **Kula ognia:** leci prosto, wybucha na pierwszym wrogu lub na końcu lotu. Ewolucja: ziemia płonie i rani dalej.
  - **Lodowa nova:** pierścień wokół gracza, obrażenia i spowolnienie. Ewolucja: zamrożenie (ogłuszenie).
  - **Łańcuch błyskawic:** trafia najbliższego wroga i skacze na kolejnych, każdy skok słabszy. Ewolucja: ogłuszenie.
  - **Teleport:** skok o kilka metrów w kierunku ruchu z chwilą nietykalności i wybuchem w miejscu lądowania. Ewolucja: wybuch także na starcie.
  - **Magiczne kule:** kule krążą wokół gracza i ranią wrogów. Ewolucja: kule strzelają w najbliższego wroga.

## 8. Wrogowie

- Zakres: **4 lub więcej typów** (ambitnie, patrz ryzyka).
- Wrogowie pojawiają się w współrzędnych `(θ, φ)` na torusie, przed graczem.
- **Typy:** biegacz (szybki, słaby), strzelec (atak z dystansu), pancerny (wolny, wytrzymały), rój (wiele małych). Mini-boss na czubku głowy to opcja, jeśli zostanie czas.
- **Golem** (pancerny): wolny, 80 HP, prawie nie da się go odepchnąć. Z bliska unosi pięści (pierścień na ziemi pokazuje zasięg) i uderza w ziemię wokół siebie z dużym odrzutem, potem chwilę stoi bezbronny.
- **Nietoperze** (rój): grupy po 4-6, słabe i szybkie, krążą wokół gracza i co jakiś czas po błysku przelatują przez niego.
- **Szaman** (wsparcie): trzyma dystans, co kilka sekund leczy rannych wrogów wokół siebie i rzuca klątwę: pod graczem pojawia się pierścień, który po chwili wybucha, więc trzeba z niego wyjść. Trafienie przerywa rzucanie.
- Wygląd wszystkich wrogów w stylu LD57: bryły low poly z prostą animacją w kodzie (oddech, podskoki, kołysanie, machanie skrzydłami, nadymanie przed atakiem).
- **[DO USTALENIA]** dokładne statystyki (ustalimy na prototypie).
- Zachowania z prototypu (na bazie LD57): **biegacz** otacza gracza razem z innymi biegaczami, miga przed atakiem, wyskakuje do przodu i odskakuje. **Strzelec** trzyma dystans, chodzi bokiem, ucieka, gdy gracz jest blisko, i strzela po zapowiedzi (błysk i linia celowania). Trafienie w trakcie zapowiedzi przerywa atak wroga.
- Trudność wrogów zależy od **odległości od paszczy** (patrz sekcja 9), a z kolejnymi rundami rośnie dodatkowo mnożnik: więcej wrogów i szybsza paszcza.

## 9. Progresja

Progresja ma zmusić gracza do walki. Bez niej opłacałoby się tylko biec, więc **trudność rośnie szybciej, niż da się ją przeskoczyć samym biegiem**: gracz musi walczyć wcześnie (na łatwiejszych odcinkach), żeby zdobyć poziomy i sprzęt, a potem poradzić sobie z trudniejszymi.

- **Poziomy gracza:** wrogowie dają doświadczenie (XP), po zebraniu progu gracz awansuje. Każdy poziom zwiększa statystyki (HP, obrażenia, szybkość ataku).
  - Prototyp: próg 60 XP na poziom 2, każdy kolejny ×1,35; poziom daje +12 HP i +6% obrażeń. Przy awansie gra staje i gracz wybiera jedną z trzech kart: nowy skill, ulepszenie albo ewolucję (sekcja 7). XP dają tylko wrogowie zabici przez gracza.
- **Ekwipunek:** wrogowie (i skrzynie) zostawiają przedmioty: broń, pancerz, dodatki. Ekwipunek zmienia statystyki i może dawać prostą zdolność. **[DO USTALENIA]** liczba slotów i typów przedmiotów.
- **Trudność rośnie z czasem gry i nie ma górnej granicy.** Gra to endless runner: im dłużej trwa bieżące przejście (nie liczą się przerwy między rundami, zeruje się po śmierci), tym więcej, szybciej i silniej pojawiają się wrogowie, aż w pewnym momencie nie da się przeżyć. Trudność = `(czas / secondsPerDifficulty)^difficultyExponent` (`EnemySpawner`), poziom wroga i liczebność rosną liniowo z nią bez ograniczenia (tylko twardy limit `maxAliveLimit` i minimalny odstęp `minSpawnInterval` chronią wydajność). Poniższa lista opisuje stary pomysł (odległość od paszczy) i jest do przepisania:
  - blisko paszczy: wrogowie najsłabsi (dobre miejsce na zdobywanie XP),
  - dalej: coraz silniejsi,
  - tuż za paszczą, przy czubku głowy: najsilniejsi (dojście do wrażliwego punktu jest najtrudniejsze).
- **Paszcza osłabia segmenty, które pożera.** Gdy paszcza zbliża się do kolejnych segmentów, trudność każdego z nich spada, bo dzieli go od niej coraz mniej. Jest to też nagroda za wolne tempo: zostawanie z tyłu oznacza łatwiejszych wrogów, ale mniej czasu.
- **Kolejne rundy:** do trudności z odległości dochodzi mnożnik rundy (liczniejsi i silniejsi wrogowie, szybsza paszcza).
- **Wróg dostaje siłę raz, w momencie pojawienia się**, na podstawie trudności segmentu, w którym powstaje. Później jej nie zmienia, więc gracz może ocenić ryzyko walki. Wrogów pojawiamy blisko gracza (kilka sekund przed nim), żeby ich poziom odpowiadał miejscu, w którym naprawdę walczy.
- **Dylemat:** walka daje siłę, ale kosztuje czas, a paszcza goni. Pominięcie walk na początku oznacza słabszego gracza później.
- **Kolejne rundy:** poziom i ekwipunek **zostają** po wygranej rundzie. Trudność kolejnej rundy rośnie szybciej niż samo wzmocnienie gracza.
- **Po śmierci:** koniec przejścia, poziom i sprzęt przepadają, nowa gra zaczyna się od rundy 1 (patrz sekcja 6). To klasyczny roguelite: każda próba zaczyna od zera.
- Rozbudowana meta-progresja (waluta, drzewko ulepszeń poza rundą) jest poza zakresem.

## 10. Czytelność i interfejs

- Wskaźniki na ekranie: **kierunek i odległość do paszczy** oraz **pasek postępu okrążenia i życie wrażliwego punktu** (gdzie jest cel).
- Efekty w świecie: poświata, dźwięk i drgania kamery przy zbliżaniu się paszczy.
- UI: HP gracza, numer rundy, ekran startowy, ekran zwycięstwa rundy, ekran śmierci, pauza.
- **Ekran startowy:** krótkie ładowanie, potem menu (wybór klasy, rekord, Graj) nad torusem widzianym z góry. Po „Graj” kamera zjeżdża za plecy bohatera i dopiero wtedy gra rusza.
- **Styl UI:** lekka inspiracja kulturą aztecką, bez przesady: obsydian, turkus, złoto i czerwień paszczy, schodkowe narożniki paneli, pas schodkowego meandra, schodkowa spirala (xicalcoliuhqui), kamień słońca w tle ekranów, numer rundy cyfrą i kropkami, pasek HP jako turkusowa mozaika, pierścień okrążenia z 20 znacznikami (20 znaków dni). Projekt: canvas „Uroboros — propozycja UI” (https://claude.ai/artifact/VR4j8GK12A2xV2qYbzkG7V).
- **Ekrany w grze:** awans (karty), zwycięstwo rundy (czas okrążenia, pokonani wrogowie, najbliżej paszczy, co zmienia następna runda, przycisk „Dalej” skraca przerwę), śmierć (przyczyna, przetrwane rundy, rekord, „Zacznij od nowa” albo „Menu główne”; nowe przejście czeka na wybór gracza), pauza z ustawieniami (głośność, pełny ekran) i sterowaniem. Teksty UI, nazwy i opisy skilli są po polsku.

## 11. Styl wizualny i dźwięk

- **Grafika:** stylizowany low poly, spójna paleta, shadery i efekty cząsteczkowe podbijające prezentację (łuski, mgła, poświata paszczy). **[DO USTALENIA]** paleta i referencje.
- **Dźwięk:** muzyka rosnąca w napięciu przy zbliżaniu się paszczy, efekty walki, dźwięk paszczy. W tle zapętlony ambient („Cavernous Ritual”), ten sam w menu i w grze, bez przerwy przy nowym runie. Źródło: mieszanka darmowych paczek (CC0), pracy ręcznej i syntezatorów. Przy każdym assecie z zewnątrz sprawdzamy licencję.

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

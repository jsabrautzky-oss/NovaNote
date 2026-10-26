# NovaNote – Hilfe

NovaNote ist ein schneller Text- und Code-Editor (Notepad-Ersatz) für Windows mit Mehrfach-Tabs,
Syntax-Hervorhebung, Syntax-Check, Code-Folding, Markdown-Vorschau und einem lokalen KI-Assistenten.
Aktuelle Version: %VERSION%. Programm: WPF/.NET.
Repo/Downloads: https://github.com/jsabrautzky-oss/NovaNote
Einstellungen und Daten liegen unter %APPDATA%\NovaNote (einstellungen.json, sitzung.json, ki.json).

Nutzung dieses Wikis: get_help ohne Thema liefert das Inhaltsverzeichnis. get_help mit einem Thema
(z. B. "Folding", "Tastenkürzel", "Ansicht", "Einstellungen") liefert den passenden Abschnitt.

## Über
- NovaNote ist ein Desktop-Editor (Windows, WPF/.NET) mit Tabs und lokaler KI.
- Version: %VERSION%. Anzeigbar auch im Menü "Hilfe → Über NovaNote".
- Quellcode und Release-Downloads: https://github.com/jsabrautzky-oss/NovaNote
- Benutzereinstellungen: %APPDATA%\NovaNote\einstellungen.json; Sitzung/Tabs: sitzung.json; KI: ki.json.
- Der KI-Assistent läuft gegen einen lokalen/lokal-ähnlichen Anbieter (LM Studio, Ollama oder
  OpenAI-kompatibel), eingerichtet über "KI → KI-Einstellungen…".

## Menüs
Die Menüleiste oben enthält: Datei, Bearbeiten, Format, Ansicht, KI, Hilfe.
Rechts neben der Menüleiste ist ein Knopf zum Ein-/Ausblenden des KI-Panels.

### Datei
- Neuer Tab (Strg+T): leeren Tab anlegen.
- Öffnen… (Strg+O): Datei in neuem Tab öffnen (auch per Drag & Drop ins Fenster).
- Speichern (Strg+S): aktuelle Datei speichern.
- Speichern unter… (Strg+Umschalt+S): neuen Dateinamen/Endung wählen.
- Seiteneinrichtung…: Drucker und Ränder für den Druck festlegen.
- Drucken… (Strg+P): Dokument drucken.
- Tab schließen (Strg+W): aktuellen Tab schließen (letzter Tab schließt das Fenster).
- Zuletzt geschlossenen Tab öffnen (Strg+Umschalt+T): zuletzt geschlossenen Tab wiederherstellen (bis zu 15 gemerkt).
- Alle Tabs schließen (Strg+Umschalt+W).
- Vor dem Schließen nach Speichern fragen (Ankreuzoption): aus = offene Änderungen ohne Dialog verwerfen.
- Beenden (Alt+F4).

### Bearbeiten
- Rückgängig (Strg+Z), Wiederholen (Strg+Y oder Strg+Umschalt+Z).
- Ausschneiden (Strg+X), Kopieren (Strg+C), Einfügen (Strg+V), Löschen (Entf).
- Suchen… (Strg+F), Weitersuchen (F3), Rückwärtssuchen (Umschalt+F3), Ersetzen… (Strg+H).
  Suche ist reiner Text (Optionen Groß-/Kleinschreibung, ganze Wörter, rückwärts), KEIN regulärer Ausdruck.
- Gehe zu Zeile… (Strg+G).
- Alles markieren (Strg+A).
- Uhrzeit und Datum (F5): fügt aktuelle Zeit im Format TT.MM.JJJJ HH:mm:ss ein.

### Format
- Zeilenumbruch (Ankreuzoption): Zeilenumbruch ein/aus.
- Schriftart…: Schriftfamilie, -stil und -größe (9–72 pt) für alle Tabs.
- Kodierung: UTF-8, UTF-8 mit BOM, UTF-16 LE, UTF-16 BE, ANSI.
- Zeilenenden: Windows (CRLF), Unix (LF), Macintosh (CR).

### Ansicht
- Tab in neues Fenster verschieben (Strg+Umschalt+D).
- Tabs → Multi-Tab-Modus (Ankreuzoption): aus blendet die Tab-Leiste aus und hält nur einen Tab.
- Tabs → Tabs beim Start wiederherstellen (Ankreuzoption).
- Vergrößern (Strg+Plus), Verkleinern (Strg+Minus), Standardgröße (Strg+0). Zoom 10–1000 %;
  auch Strg+Mausrad über dem Editor; in der Statusleiste gibt es einen Zoom-Regler.
- Statusleiste (Ankreuzoption): untere Statuszeile ein/aus.
- Zeilennummern (Ankreuzoption): Nummernleiste links ein/aus.
- Whitespace anzeigen (Ankreuzoption): Leerzeichen/Tabs/Zeilenenden sichtbar.
- Syntax-Check (Ankreuzoption): Hintergrund-Syntaxprüfung ein/aus.
- Code-Folding (Ankreuzoption): Ein-/Ausklappen von Codeblöcken ein/aus.
  Folding lässt sich hier abschalten; alternativ über das Rechtsklick-Menü im Editor.
- Nächster Syntaxfehler (F4): springt zur nächsten markierten Fehlerstelle (nur bei Fehlern aktiv).
- Farbschema: Hell, Dunkel, System (passt Fenstertitelleiste mit an).
- Sprachen: "Automatisch erkennen"; "JSON formatieren" (Strg+Umschalt+J); "Code formatieren"
  (Strg+Umschalt+F, nur Python/C#); "Python-Completion" (Strg+Leertaste);
  dann die Sprachen Text, JSON, XML, HTML, CSS, JavaScript, C++, Markdown, Python, C#, YAML.
  Markdown zeigt automatisch eine Vorschau als Seitenbereich.

### KI
- KI-Panel (Strg+Alt+K): rechtes Chat-Panel ein/aus.
- Modelle scannen: Modelle des aktiven Anbieters neu einlesen.
- KI-Einstellungen…: Anbieter, Modell, Basis-URL, API-Key, Kontextlänge, Vision, Tools verwalten.

### Hilfe
- Update → "Automatisch auf Updates prüfen" (Ankreuzoption).
- Update → "Nach Update prüfen": manuell nach neuer Version suchen.
- Debug-Log (Ankreuzoption): schreibt alle Aktionen, den kompletten KI-Chat (Eingaben, Antworten, Tool-Aufrufe und -Ergebnisse) und Fehler mit Zeitstempel in die Datei debug.log (im Datenordner %APPDATA%\NovaNote). Standardmäßig aus; über diese Ankreuzoption an/aus.
- Debug-Log öffnen: öffnet den Log-Ordner im Explorer mit ausgewählter debug.log.
- Über NovaNote: Version, .NET/Velopack-Status, GitHub-Links.

## Tastenkürzel
- Datei: Strg+T neu, Strg+O öffnen, Strg+S speichern, Strg+Umschalt+S speichern unter,
  Strg+P drucken, Strg+W Tab schließen, Strg+Umschalt+T letzten Tab wiederherstellen,
  Strg+Umschalt+W alle Tabs schließen, Alt+F4 beenden.
- Bearbeiten: Strg+Z rückgängig, Strg+Y / Strg+Umschalt+Z wiederholen, Strg+X/C/V schneiden/kopieren/einfügen,
  Entf löschen, Strg+A alles markieren, Strg+F suchen, F3 weitersuchen, Umschalt+F3 rückwärts,
  Strg+H ersetzen, Strg+G zu Zeile, F5 Datum/Zeit.
- Ansicht/Zoom: Strg+Plus vergrößern, Strg+Minus verkleinern, Strg+0 Standardgröße, Strg+Mausrad zoomen.
- Tabs: Strg+Tab nächster Tab, Strg+Umschalt+Tab vorheriger Tab, Strg+1…Strg+8 Tab 1–8,
  Strg+9 letzter Tab, Strg+Umschalt+D Tab in eigenes Fenster.
- Formate: Strg+Umschalt+J JSON formatieren, Strg+Umschalt+F Code formatieren (Python/C#),
  Strg+Leertaste Python-Completion.
- Sonstiges: F4 nächster Syntaxfehler, Strg+Alt+K KI-Panel um/aus.

## Editor-Funktionen
### Code-Folding
- An/Aus über "Ansicht → Code-Folding" (global) oder über das Rechtsklick-Menü des Editors (pro Dokument).
- Zusammenklappbare Bereiche (Klammern, bei JSON auch eckige Klammern, Kommentare; XML/HTML und YAML eigene Strategien)
  erscheinen als Marginal-Symbole. Zum Ein-/Ausklappen auf das Symbol in der Folding-Leiste klicken.
- Zum Ausschalten einfach den Haken bei "Ansicht → Code-Folding" entfernen.

### Zeilenumbruch
- Umschalten über "Format → Zeilenumbruch". An = lange Zeilen brechen um (kein horizontales Scrollen).

### Zeilennummern
- Umschalten über "Ansicht → Zeilennummern".

### Whitespace anzeigen
- Umschalten über "Ansicht → Whitespace anzeigen" (Leerzeichen, Tabs, Zeilenenden als Symbole).

### Zoom
- "Ansicht → Vergrößern/Verkleinern/Standardgröße" (Strg+Plus/Minus/0), Strg+Mausrad oder Zoom-Regler
  in der Statusleiste. Bereich 10–1000 %.

### Syntax-Check und Syntaxfehler
- "Ansicht → Syntax-Check" schaltet die Prüfung ein/aus. Fehler werden rot unterkringelt und in der
  Statusleiste gemeldet. F4 springt zum nächsten Fehler. Python nutzt bei verfügbarem Werkzeug
  Ruff/Black/autopep8 bzw. einen Python-Sprachserver.

### Schriftart
- "Format → Schriftart…" wählt Familie, Stil und Größe (9–72 pt) und gilt für alle Tabs.

### Markdown-Vorschau
- Bei Markdown-Dokumenten erscheint automatisch ein Vorschau-Bereich rechts;
  über das Rechtsklick-Menü "Markdown-Vorschau aktualisieren".

### Kodierung und Zeilenenden
- "Format → Kodierung" (UTF-8, UTF-8 mit BOM, UTF-16 LE, UTF-16 BE, ANSI) und
  "Format → Zeilenenden" (Windows CRLF, Unix LF, Macintosh CR). Beim Öffnen wird erkannt, beim Speichern angewandt.

### Sprachen / Syntax-Hervorhebung
- "Ansicht → Sprachen". "Automatisch erkennen" nutzt Dateiendung und Inhalt. Manuelle Sprachen:
  Text, JSON, XML, HTML, CSS, JavaScript, C++, Markdown, Python, C#, YAML.

### Drucken
- "Datei → Drucken…" (Strg+P) mit Rändern aus "Datei → Seiteneinrichtung…".

## Einstellungen
Die Datei %APPDATA%\NovaNote\einstellungen.json enthält (jeweils mit Umschaltort im Menü):
- RestoreSession: Tabs/Fenster beim Start wiederherstellen (Ansicht → Tabs → Tabs beim Start wiederherstellen).
- AskSaveOnClose: vor dem Schließen nach Speichern fragen (Datei-Menü).
- MultiTab: Mehrfach-Tab-Modus (Ansicht → Tabs → Multi-Tab-Modus).
- LanguageAuto: Sprache automatisch erkennen (Ansicht → Sprachen → Automatisch erkennen).
- ShowLineNumbers: Zeilennummern (Ansicht → Zeilennummern).
- ShowWhitespace: Whitespace anzeigen (Ansicht → Whitespace anzeigen).
- SyntaxCheck: Hintergrund-Syntax-Check (Ansicht → Syntax-Check).
- Folding: Code-Folding (Ansicht → Code-Folding).
- Theme: Farbschema "hell", "dunkel" oder "system" (Ansicht → Farbschema).
- UpdateCheck: automatisch auf Updates prüfen (Hilfe → Update).
- DismissedUpdateVersion: Version, deren Update-Hinweis abgelehnt wurde.
- AiPanelOpen: KI-Panel sichtbar (KI → KI-Panel / Strg+Alt+K).
- AiPanelWidth: Breite des KI-Panels in Pixeln (über den Trennstreifen).
- DebugLog: Debug-Log aktiviert (Hilfe → Debug-Log); schreibt Aktionen/KI-Chat/Fehler mit Zeitstempel in debug.log. Standardmäßig aus (nur in Entwicklungs-Builds an).

## Dialoge
- Suchen/Ersetzen: Suchfeld, Ersetzen-Feld, Optionen Groß-/Kleinschreibung, ganze Wörter, rückwärts;
  Buttons Suchen, Ersetzen, Alle ersetzen, Schließen (Strg+F / Strg+H).
- Gehe zu Zeile: Zeilennummer mit Bereichsprüfung (Strg+G).
- Schriftart: Familie, Stil, Größe mit Vorschau.
- KI-Einstellungen…: Anbieter anlegen/löschen, Typ (LM Studio, Ollama, OpenAI-kompatibel, Eigener),
  Basis-URL, API-Key, Modell-Tabelle (Name, Modell-ID, Kontext, Vision, Tools), Modelle scannen,
   Modell manuell hinzufügen, Kontextlänge/Vision je Modell überschreiben.
- Sampler je Modell: in „KI-Einstellungen…“ unter der Modelliste die Werte Temp, Top-P, Min-P und Repeat Penalty pro Modell setzbar; leer gelassene Felder sendet NovaNote nicht (der Server/das Modell nutzt seinen Standard). Das Dropdown „Vorlagen:“ setzt bewährte Kombinationen für das markierte Modell: „Präzises Coding & Logik“ 0,1 / 0,90 / 0,05 / 1,0 – „Ausgewogener Standard-Chat“ 0,7 / 0,95 / 0,05 / 1,1 – „Kreatives Schreiben / RP“ 1,1 / 1,00 / 0,10 / 1,15 – „Alle leer (Server-Standard)“ löscht alle vier Werte. Nach der Eingabe (Enter oder Tab) bleibt das Modell ausgewählt.
- Denken je Modell: Dropdown „Denken:“ unter den Sampler-Feldern setzt reasoning_effort pro Modell (off, minimal, low, medium, xhigh, adaptive); „keine – Server-Standard“ sendet das Feld gar nicht. Ollama bekommt den Wert im `options`-Objekt, andere Anbieter top-level. Die Auswahl oben rechts im KI-Panel übersteuert diese Modell-Vorgabe.
- Favoriten verwalten: gespeicherte KI-Eingaben bearbeiten.
- Seiteneinrichtung: Drucker und vier Ränder (Zoll).
- Über NovaNote: Versionsinfo und GitHub-Links.
- Update-Fortschritt: Download-/Installationsfortschritt.

## Statusleiste
Unten (anzklickbar) von links nach rechts:
- Zeile/Spalte (Cursorposition).
- Auswahl (Anzahl Zeichen, nur bei Auswahl).
- Syntax-Status (klick = Syntax-Check umschalten).
- Sprache (klick = Sprachmenü; zeigt "· Automatik" bei Auto-Erkennung).
- Kodierung (klick = Kodierungsmenü).
- Zeilenenden (klick = Zeilenendenmenü).
- Zoom in Prozent (klick = Zoom-Regler).

## KI-Panel
- Rechts neben dem Editor; öffnen mit Strg+Alt+K oder "KI → KI-Panel".
- Oben Anbieter- und Modellauswahl plus Denken-Dropdown (Denktiefe), darunter Statuszeile, dann der Chat.
- Denken-Dropdown im Panel: regelt reasoning_effort für alle Nachrichten – „Modell-Standard“ nutzt die Vorgabe aus den Modelleinstellungen, sonst wird der gewählte Wert gesendet (off/minimal/low/medium/xhigh/adaptive). Die Auswahl bleibt gespeichert und übersteuert die Modell-Vorgabe.
- Eingabezeile unten mit: Bild anhängen (+), Senden, Abbrechen, Verlauf löschen,
  Favoriten, Eingabe-Verlauf, Favoriten verwalten. Enter = senden, Umschalt+Enter = neue Zeile,
  Strg+V = Bildschirmfoto anhängen, Esc = laufende Anfrage abbrechen.
- Codeblöcke haben "Kopieren" und "In Editor einfügen".
- Editor-Aktionen der KI werden sofort ausgeführt (keine Bestätigungs-Karte); der Tool-Card zeigt nur Status und Ergebnis. Ein RÜCKGÄNGIG (Strg+Z) im Editor macht die KI-Änderung bei Bedarf wieder rückgängig.
- Unten ein Token-/Kontextzähler; bei vollem Kontext werden ältere Nachrichten automatisch gekürzt.
- Bei Denk-Modellen läuft der Denkprozess in einem abgesetzten Block (🧠 „Denken“) über der Antwort. Der Block ist standardmäßig zugeklappt und nennt im Summary die Zeichenanzahl. Ein Klick darauf öffnet ihn: Das Denken läuft dort live weiter (der Text wächst, blinkender Cursor, Auto-Scroll) – auch nach Beginn der Antwort. Noch ein Klick klappt ihn wieder zu, der Inhalt bleibt stehen.
- Werkzeugkarten erscheinen schon während des Antwortstroms: Name sofort, darunter wachsen die Argumente mit („✍️ schreibt…“); ist der Aufruf vollständig, wird er ausgeführt (⏳ Läuft…, ✅ Abgeschlossen, ❌ Fehler). Der Card enthält vollständige Argumente und Ergebnis. Ein mit Esc beendeter Lauf markiert noch offene Karten mit „⏹ Abgebrochen“.
- Runden-Marke: ab der zweiten Runde steht im Chat eine Linie „Runde N“. Daran siehst du, wie viele Schleifenschritte ein Lauf schon hat.
- Wiederholt die KI exakt denselben Schritt (gleiches Werkzeug, gleiche Argumente), bekommt die Karte einen gelben Rand und den Vermerk „🔁 2. Mal“ – ein Hinweis zum Eingreifen (Esc), kein automatischer Abbruch.
- Die untere „KI denkt“-Leiste ist eine Live-Statuszeile: Zustand (KI denkt / schreibt Werkzeug / Werkzeug läuft / antwortet), Runde, Schrittanzahl, Laufzeit in Sekunden, Tokenstand und einzeilig der letzte Denksatz. Kommen über 15 Sekunden keine Daten, warnt sie „keine Daten seit … s – mit Esc abbrechen“.
- Läuft ein KI-Lauf, zeigt Windows in der Taskleiste unter dem NovaNote-Icon einen animierten Fortschrittsbalken (Fehler: rot, nach Esc: gelb, je 6 s). Windows zeigt ihn nur, wenn das NovaNote-Icon in der Taskleiste angeheftet ist.
- Multi-Tab: Die KI weiß, dass es mehrere offene Tabs gibt und der Nutzer Tabs jederzeit öffnen/schließen/wechseln kann. Sie sieht alle offenen Tabs (der aktive ist markiert) und merkt Dokument-/Tab-Wechsel. Ihre Werkzeuge wirken aber nur auf das aktive Dokument — für ein anderes muss der Nutzer erst den Tab wechseln.
- Ein KI-Lauf ist an das Dokument gebunden, mit dem er gestartet ist: Auch wenn du während der Antwort den Tab wechselst, schreibt die KI weiter in das Start-Dokument und nicht in den neuen. Die KI arbeitet ungehindert: Sie ruft so viele Werkzeuge und Schritte auf, wie sie braucht – ohne Schritt-Limit, ohne Überspringen wiederholter Schritte und ohne automatischen Abbruch. All das ist im Chat sichtbar (Runden-Marken, 🔁-Vermerk, Statuszeile). Esc bricht einen laufenden KI-Lauf ab; wird das Start-Dokument geschlossen, endet der Lauf.
- Die KI kann Dokument lesen/durchsuchen, Sprache setzen, Titel vergeben, navigieren/markieren, Text ersetzen/einfügen,
  Syntax prüfen und Code formatieren. Sie kennt diese Anleitung und hilft bei Bedienfragen (get_help).
- Titel: Ein neues, leeres Dokument heißt „Unbenannt". Erzeugt die KI Inhalt (Liste, Aufsatz, Code), gibt sie dem Dokument mit `set_document_title` selbstständig einen kurzen Titel – oder auf Bitte („gib ihm einen Titel"). Der Titel wird beim ersten Speichern zum Dateinamen-Vorschlag. Bei einer bereits gespeicherten Datei kann die KI den Titel nicht ändern (der kommt vom Dateinamen).

## Kommandozeile
- NovaNote datei.txt …: Datei(en) beim Start öffnen.
- /N: neue Instanz starten (statt vorhandenes Fenster zu nutzen).
- /S x y w h: Fensterposition und -größe beim Start setzen.
- /?: kurze Hilfe anzeigen.

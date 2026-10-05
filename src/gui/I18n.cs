// RPMac - Internationalization (i18n)
// Copyright (C) 2026 golirino - GPL-2.0-only. Ver LICENSE y NOTICE.
//
// Lightweight string lookup: no .resx files, no Visual Studio, no dependencies.
// Keys are the English strings themselves, so the existing code reads naturally
// and the fallback is always the original text.
//
// Usage:
//   I18n.SetLanguage("it");           // set current language
//   Text = I18n.T("Settings");        // get translated string
//   I18n.T("Fan {0}: ...", n, ...);   // format with args

using System;
using System.Collections.Generic;

namespace RPMac {

    static class I18n {

        static string _lang = "en";

        // English is the source language — the key itself IS the English text,
        // so the English dictionary is built implicitly (fallback returns the key).
        static readonly Dictionary<string, string> IT = new Dictionary<string, string> {
            // ---- Nav rail ----
            { "Fans", "Ventole" },
            { "Sensors", "Sensori" },
            { "Presets", "Preset" },
            { "Settings", "Impostazioni" },

            // ---- Fan modes ----
            { "Auto", "Auto" },
            { "Max", "Max" },
            { "Manual", "Manuale" },
            { "Curve", "Curva" },

            // ---- Fan card ----
            { "RPM", "RPM" },
            { "Sensor", "Sensore" },
            { "Apply RPM", "Applica RPM" },
            { "Apply curve", "Applica curva" },
            { "Copy to all fans", "Copia in tutte le ventole" },
            { "Highest temp (any sensor)", "Temp max (ogni sensore)" },
            { "{0:0}–{1:0} RPM range", "{0:0}–{1:0} range RPM" },
            { "Target {0:0} RPM · {1}", "Target {0:0} RPM · {1}" },
            { "controlled by RPMac", "controllato da RPMac" },
            { "controlled by the Mac", "controllato dal Mac" },

            // ---- Curve editor ----
            { "Drag a point to move it · double-click the graph to add one · right-click a point to remove it",
              "Trascina un punto per spostarlo · doppio clic sul grafico per aggiungerne uno · clic destro su un punto per rimuoverlo" },
            { "A curve needs at least two points.", "Una curva richiede almeno due punti." },
            { "Curve: needs at least two points.", "Curva: richiede almeno due punti." },
            { "Curve: pick a sensor first.", "Curva: seleziona prima un sensore." },
            { "Fan {0}: curve on · {1} · {2} points, {3:0}–{4:0} RPM", "Ventola {0}: curva attiva · {1} · {2} punti, {3:0}–{4:0} RPM" },
            { "Curve copied to {0} other fan.", "Curva copiata in {0} altra ventola." },
            { "Curve copied to {0} other fans.", "Curva copiata in altre {0} ventole." },
            { "{0} → {1:0}", "{0} → {1:0}" },

            // ---- History graph ----
            { "Last 5 minutes", "Ultimi 5 minuti" },
            { "Hottest sensor", "Sensore più caldo" },
            { "Fan RPM", "RPM ventola" },
            { "Hottest sensor  ", "Sensore più caldo  " },
            { "Fan RPM  ", "RPM ventola  " },

            // ---- Sensors page ----
            { "Live temperatures", "Temperature live" },
            { "All sensors (raw)", "Tutti i sensori (raw)" },
            { "No sensors detected.", "Nessun sensore rilevato." },
            { "None detected", "Non rilevati" },
            { "No known sensors detected on this Mac.", "Nessun sensore noto rilevato su questo Mac." },
            { "Every temperature key the SMC reports, including ones RPMac can't name.",
              "Ogni chiave di temperatura riportata dal SMC, incluse quelle che RPMac non sa nominare." },
            { "Some Macs expose sensors RPMac doesn't have a name for. Give one a name and it becomes a normal sensor: usable in curves, \"Highest temp\", the overlay, the tray and the CSV.",
              "Alcuni Mac espongono sensori per cui RPMac non ha un nome. Assegnane uno e diventerà un sensore normale: utilizzabile in curve, \"Temp max\", overlay, tray e CSV." },
            { "The list fills in once you press \"Show all sensors (raw)\" above.",
              "L'elenco si compila dopo aver premuto \"Mostra tutti i sensori (raw)\" sopra." },
            { "Detecting sensors… (raw, unverified list)", "Rilevamento sensori… (elenco raw non verificato)" },

            // ---- Custom sensor ----
            { "Name a sensor of your own", "Dai un nome a un sensore" },
            { "Add", "Aggiungi" },
            { "Remove", "Rimuovi" },
            { "Press \"Show all sensors (raw)\" first, then pick a key.",
              "Premi prima \"Mostra tutti i sensori (raw)\", poi scegli una chiave." },
            { "Give the sensor a name first.", "Dai prima un nome al sensore." },
            { "Show all sensors (raw)", "Mostra tutti i sensori (raw)" },

            // ---- Settings page ----
            { "Start with Windows", "Avvia con Windows" },
            { "Automatically opens the app at sign-in.", "Apre automaticamente l'app all'accesso." },
            { "Start minimized to tray", "Avvia minimizzato nel tray" },
            { "Launch hidden in the system tray (next to the clock).",
              "Avvia nascosto nel tray di sistema (accanto all'orologio)." },
            { "Show temperatures in °F", "Mostra temperature in °F" },
            { "Display temperatures in Fahrenheit instead of Celsius.",
              "Mostra le temperature in Fahrenheit invece di Celsius." },
            { "Smooth fan changes", "Variazioni graduali delle ventole" },
            { "Ignore tiny temperature wobbles and ease the fan down slowly, so a curve doesn't make it audibly hunt up and down.",
              "Ignora piccole oscillazioni di temperatura e riduce la ventola lentamente, così una curva non la fa cercare su e giù in modo udibile." },

            // ---- Emergency cooling ----
            { "Emergency cooling", "Raffreddamento di emergenza" },
            { "If any sensor reaches {0}, every fan goes to maximum until it cools down — whatever mode it's in.",
              "Se un sensore raggiunge {0}, tutte le ventole vanno al massimo finché non si raffredda — in qualsiasi modalità siano." },
            { "Trigger at", "Attiva a" },

            // ---- Emergency shutdown ----
            { "Emergency shutdown", "Spegnimento di emergenza" },
            { "Shut down at", "Spegni a" },
            { "Also shut down if a fan stalls", "Spegni anche se una ventola si ferma" },
            { "Below", "Sotto" },
            { "Command", "Comando" },
            { "shutdown.exe", "shutdown.exe" },
            { "For a machine nobody is watching. If the hottest sensor stays at {0} or above for {1} seconds — with the fans already at maximum — Windows is told to shut down cleanly instead of waiting for the CPU to cut power on its own.",
              "Per un computer non sorvegliato. Se il sensore più caldo resta a {0} o più per {1} secondi — con le ventole già al massimo — Windows viene spento in modo pulito invece di aspettare che la CPU tagli l'alimentazione da sola." },
            { "A dying fan can cook the machine long before any one sensor hits the limit above. This only counts a fan as stalled when it is being asked to spin faster than {0} RPM and reads below it anyway, for {1} seconds — so a fan that is idle on purpose never triggers it.",
              "Una ventola guasta può surriscaldare il computer molto prima che un sensore raggiunga il limite sopra. Una ventola viene considerata ferma solo quando le viene chiesto di girare a più di {0} RPM ma legge comunque sotto, per {1} secondi — così una ventola intenzionalmente ferma non lo attiva mai." },
            { "/s shuts down, /f forces programs to close, /t 30 waits 30 seconds first. During that wait you can still cancel it from a command prompt with  shutdown /a . Every shutdown RPMac starts is written to shutdown.log next to the CSV, so you can see why it happened.",
              "/s spegne, /f forza la chiusura dei programmi, /t 30 aspetta prima 30 secondi. Durante l'attesa puoi ancora annullarlo dal prompt dei comandi con  shutdown /a . Ogni spegnimento avviato da RPMac viene registrato in shutdown.log accanto al CSV, così puoi vedere perché è successo." },

            // ---- CSV logging ----
            { "Record to a CSV file", "Registra su file CSV" },
            { "Append every reading to history.csv, so you can look back at what ran hot during a game or a long render.",
              "Aggiunge ogni lettura a history.csv, per poter rivedere cosa si è surriscaldato durante un gioco o un render lungo." },
            { "Open the folder", "Apri la cartella" },
            { "Couldn't open the folder: ", "Impossibile aprire la cartella: " },

            // ---- Tray ----
            { "Show in tray", "Mostra nel tray" },
            { "Choose what the tray icon displays — the app icon, nothing, or a live temperature.",
              "Scegli cosa mostra l'icona nel tray — l'icona dell'app, niente, o una temperatura live." },
            { "App Icon", "Icona app" },
            { "None", "Niente" },
            { "Highest Temp", "Temp max" },
            { "Open", "Apri" },
            { "Quit", "Esci" },
            { "(no presets yet)", "(nessun preset)" },
            { "Toggle Overlay", "Attiva/disattiva overlay" },

            // ---- Overlay ----
            { "On-screen overlay", "Overlay a schermo" },
            { "Show fan RPM and temperatures on top of everything (top-right corner).",
              "Mostra RPM ventole e temperature sopra tutto (angolo in alto a destra)." },
            { "Overlay layout", "Layout overlay" },
            { "Vertical", "Verticale" },
            { "Horizontal", "Orizzontale" },
            { "Show in overlay", "Mostra nell'overlay" },

            // ---- Presets page ----
            { "Save your current fan setup as a profile and switch with one click.",
              "Salva la configurazione attuale delle ventole come profilo e cambialo con un clic." },
            { "SAVE CURRENT SETUP", "SALVA CONFIGURAZIONE" },
            { "Profile name (e.g. Gaming)", "Nome profilo (es. Gaming)" },
            { "No profiles yet. Set your fans up on the Fans page, then save the setup here.",
              "Nessun profilo. Configura le ventole nella pagina Ventole, poi salvi qui la configurazione." },
            { "Save", "Salva" },
            { "Active", "Attivo" },
            { "Applied preset: ", "Preset applicato: " },
            { "Type a name for the preset first.", "Digita prima un nome per il preset." },
            { "Saved preset: ", "Preset salvato: " },
            { "Deleted preset: ", "Preset eliminato: " },

            // ---- Themes ----
            { "Theme", "Tema" },
            { "Theme: ", "Tema: " },
            { "Dark", "Scuro" },
            { "Light", "Chiaro" },
            { "Nature", "Natura" },
            { "Japan", "Giappone" },

            // ---- Status bar ----
            { "Starting…", "Avvio…" },
            { "Read-only on this hardware — ", "Sola lettura su questo hardware — " },
            { "Driver OK · ", "Driver OK · " },
            { "Resumed — settings reapplied · ", "Ripreso — impostazioni riapplicate · " },
            { "SMC recovered — settings reapplied · ", "SMC ripristinato — impostazioni riapplicate · " },
            { "SMC not responding — retrying · ", "SMC non risponde — nuovo tentativo · " },
            { "SMC not responding — change not applied. Retrying automatically.",
              "SMC non risponde — modifica non applicata. Nuovo tentativo automatico." },
            { "Start with Windows: enabled", "Avvio con Windows: attivo" },
            { "Start with Windows: disabled", "Avvio con Windows: disattivo" },
            { "Start minimized: on", "Avvio minimizzato: attivo" },
            { "Start minimized: off", "Avvio minimizzato: disattivo" },
            { "Temperatures: °F", "Temperature: °F" },
            { "Temperatures: °C", "Temperature: °C" },
            { "Smoothing: on", "Variazioni graduali: attive" },
            { "Smoothing: off", "Variazioni graduali: disattive" },
            { "Emergency cooling: on", "Raffreddamento di emergenza: attivo" },
            { "Emergency cooling: off", "Raffreddamento di emergenza: disattivo" },
            { "Emergency shutdown: on", "Spegnimento di emergenza: attivo" },
            { "Emergency shutdown: off", "Spegnimento di emergenza: disattivo" },
            { "Fan-stall shutdown: on", "Spegnimento per ventola ferma: attivo" },
            { "Fan-stall shutdown: off", "Spegnimento per ventola ferma: disattivo" },
            { "Recording stopped", "Registrazione fermata" },
            { "Overlay: on", "Overlay: attivo" },
            { "Overlay: off", "Overlay: disattivo" },
            { "Show in tray: ", "Mostra nel tray: " },
            { "Error: ", "Errore: " },

            // ---- Emergency shutdown messages ----
            { "EMERGENCY SHUTDOWN — ", "SPEGNIMENTO DI EMERGENZA — " },
            { "Emergency shutdown was triggered but shutdown.exe failed: ",
              "Spegnimento di emergenza attivato ma shutdown.exe ha fallito: " },
            { "RPMac is shutting this PC down", "RPMac sta spegnendo questo PC" },

            // ---- Read-only / errors ----
            { "⚠  Read-only mode", "⚠  Modalità sola lettura" },
            { "Couldn't open the I/O driver (InpOut).\nRun the app as administrator.",
              "Impossibile aprire il driver I/O (InpOut).\nEsegui l'app come amministratore." },

            // ---- Sensor group names ----
            { "CPU", "CPU" },
            { "GPU", "GPU" },
            { "SYSTEM", "SISTEMA" },

            // ---- Sensor labels (CURATED) ----
            { "CPU (die)", "CPU (die)" },
            { "CPU (heatsink)", "CPU (dissipatore)" },
            { "CPU (proximity)", "CPU (prossimità)" },
            { "CPU (PECI)", "CPU (PECI)" },
            { "CPU A (die)", "CPU A (die)" },
            { "CPU A (heatsink)", "CPU A (dissipatore)" },
            { "CPU B (die)", "CPU B (die)" },
            { "CPU B (heatsink)", "CPU B (dissipatore)" },
            { "GPU (PECI)", "GPU (PECI)" },
            { "GPU 1 (die)", "GPU 1 (die)" },
            { "GPU 1 (heatsink)", "GPU 1 (dissipatore)" },
            { "GPU 1 (proximity)", "GPU 1 (prossimità)" },
            { "GPU 2 (die)", "GPU 2 (die)" },
            { "GPU 2 (heatsink)", "GPU 2 (dissipatore)" },
            { "GPU 2 (proximity)", "GPU 2 (prossimità)" },
            { "Memory", "Memoria" },
            { "Memory slot", "Slot memoria" },
            { "Memory 2", "Memoria 2" },
            { "Ambient", "Ambiente" },
            { "Ambient 2", "Ambiente 2" },
            { "Power (PCH)", "Potenza (PCH)" },
            { "Hard drive", "Hard disk" },
            { "Northbridge (die)", "Northbridge (die)" },
            { "Northbridge (proximity)", "Northbridge (prossimità)" },
            { "Northbridge (heatsink)", "Northbridge (dissipatore)" },
            { "Thunderbolt", "Thunderbolt" },
            { "Battery", "Batteria" },
            { "Wi-Fi", "Wi-Fi" },

            // ---- Language setting ----
            { "Language", "Lingua" },
            { "Choose the interface language.", "Scegli la lingua dell'interfaccia." },

            // ---- Status bar fragments ----
            { "Added ", "Aggiunto " },
            { "Removed ", "Rimosso " },
            { "FAN ", "VENTOLA " },
            { " isn't reading a temperature right now.", " non sta leggendo una temperatura in questo momento." },
            { " · hottest: ", " · più caldo: " },
            { " sensors", " sensori" },
            { " · updated ", " · aggiornato " },
            { "   ·   ", "   ·   " },
            { " fan", " ventola" },
            { " fans", " ventole" },
            { "Apply", "Applica" },
            { "Curve ", "Curva " },
            { "Fan ", "Ventola " },
            { ". Run  shutdown /a  to cancel.", ". Esegui  shutdown /a  per annullare." },
            { "\r\nRun  shutdown /a  to cancel.", "\r\nEsegui  shutdown /a  per annullare." },

            // ---- Format strings (temperature) ----
            // These contain format placeholders and are used as-is in both languages
            // (the °C/°F and numbers are universal)
        };

        public static void SetLanguage(string lang) {
            if (lang == null) lang = "en";
            lang = lang.ToLowerInvariant();
            if (lang != "en" && lang != "it") lang = "en";
            _lang = lang;
        }

        public static string CurrentLanguage { get { return _lang; } }

        static Dictionary<string, string> CurrentDict {
            get {
                if (_lang == "it") return IT;
                return null;  // en: key is the value
            }
        }

        /// <summary>
        /// Get the translated string for the given key (the English text).
        /// Falls back to the key itself if no translation is found.
        /// </summary>
        public static string T(string key) {
            if (key == null) return null;
            var dict = CurrentDict;
            if (dict != null) {
                string val;
                if (dict.TryGetValue(key, out val)) return val;
            }
            return key;  // fallback: English
        }

        /// <summary>
        /// Get a translated format string and apply string.Format with the given args.
        /// </summary>
        public static string T(string key, params object[] args) {
            return string.Format(T(key), args);
        }

        /// <summary>
        /// Returns true if the given language code is supported.
        /// </summary>
        public static bool IsSupported(string lang) {
            return lang == "en" || lang == "it";
        }
    }
}
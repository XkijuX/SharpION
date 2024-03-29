# SharpION

![SharpionLogo](http://65.108.213.178:3000/api/image?imageID=Njf1nF2r9VnrQWuRb8c3)
SharpION er et Onion Nettverk implementert i C#. SharpION fungerer som en tunnel mellom en klient og flere servere. Dette gjøres ved at brukeren kobler seg på SharpION nettverket. Brukeren bruker programmet Client som er en inngang til SharpION nettverket og fungerer som en proxy. Denne proxyen kan brukes med nettlesere som blant annet [Google Chrome](https://www.google.com/chrome/) ved bruk av utvidelser. 

Når proxy serveren får et request vil en ny rute igjennom SharpION nettverket opprettes. Det velges tre noder fra nettverket. Deretter skapes sikre veier mellom disse nodene ved hjelp av kryptering. 

## **Innhold**
---
1. [Funksjonalitet](#funksjonalitet)
1. [Fremtidig arbeid / mangler](#fremtidig-arbeid--mangler--svakheter)
1. [Eksterne avhengigheter](#eksterne-avhengigheter)
1. [Intallasjonsinstrukser](#intallasjonsinstrukser)
1. [Bruker Manual](#bruker-manual)
1. [Running Tests](#kjøring-av-tester)
1. [Kryptografi](#kryptografi)
1. [Ekstern infomasjon](#ekstern-informasjon)


<br/><br/>
## **Funksjonalitet**
---
- Sending av HTTP request
    - Støtter http metoder som GET, PUT, POST, DELETE. (CONNECT er ikke støttet)
- Støtte for visuell fremvisning
    - Kan brukes igjennom chrome og andre nettlesere som støtter proxy for HTTP koblinger.
- Kryptografi
    - Bruker RSA og AES-CFB for å skjule meldingene i nettverket.
    - [For mer info om kryptografi](#kryptografi)
- DNS lookup
    - Henter ip adresser fra DNS servere fra HTTP header. Dette gjøres ved endenoden og sikkrer anonymitet hos bruker.

<br/><br/>

## **Fremtidig arbeid / mangler / svakheter**
---
SharpION er et program som ikke er ferdig utviklet. Programmet har fortsatt mangler og svakheter. Disse er som følger:
- 100% Test dekning (``mangler / fremtidig arbeid``)
    - Viktig for å sikre at programmet og funksjoner fungerer.
- Onion Routing timing analyser (``svakhet``)
    - Selv om onion routing fungerer bra for å skjule data er det ikke perfekt. Ved hjelp av timing analyser er det mulig å finne avsender og destinasjon. En måte å løse dette på er å bruke Garlic Routing.
- HTTPS (``mangel / fremtidig arbeid``)
    - SharpION støtter bare HTTP nå som ikke er sikkert på endenoden. Dersom en ondsinnet person kjører en node som blir endenoden kan denne personen fange trafikken som går igjennom nettverket.
- Andre Nettverkspakker (``mangel / fremtidig arbeid``)
    - SharpION kan fungere som et VPN nettverk som støtter alle pakker som sendes fra maskiner.
- Støtte for ipaddresser (``mangel / fremtidig arbeid``)
    - SharpION støtter ikke urler i form av ip adresser.
- Proxy utvidelse (``svakhet``)
    - Ved å bruke en proxy utvidelse i chrome gir man utvidelsen tilgang til all data. Dette kan være en svakhet hvis utvikleren har onde hensikter. Problemet kan fikses ved å bruke windows proxy, men dette støttes ikke av program slik den er i dag. Derfor er den anbefalte proxy tjenesten valgt til dette programmet open source som gir mer trygghet.
- Utsjekking av Noder (``svakhet / mangel / fremtidig arbeid``)
    - Dersom en node avsluttes må programmet startes på nytt. Dette er derfor en svakhet og mangel som er viktig å få gjort.
<br/><br/>

## **Eksterne avhengigheter**
---
Sharpion sine avhengigheter er som følger:
- Client
    - [Newtonsoft.Json](https://www.newtonsoft.com/json) 
        - JSON parser for C#.
        - Brukes for å lese JSON som kommer fra DirectoryServer rest APIet. 
- DirectoryServer
    - [Express](https://expressjs.com/)
        - Web rammeverk for Node.js
        - Brukes for å lage et rest API for å holde kontroll på nodene i SharpION nettverket.
    - [Jest](https://jestjs.io/)
        - Test rammeverk for Node.js
        - Brukes for å teste directory server.
    - [SuperTest](https://github.com/visionmedia/supertest#readme)
        - Modul for testing av HTTP (brukes for å kjøre test forespørlser til express serveren)
- Node (Bruker ingen eksterne avhengigheter)

<br/><br/>
## **Installasjonsinstrukser**
---
For å kjøre programmene kreves både dotnet og node.js.
- Installasjons linker:
    - [dotnet](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
    - [node.js](https://nodejs.org/en/download/)

For å installere dependencies for Client:
```bash
    cd Client
    dotnet restore
```

For å installere dependencies på Directory Server:
- ``npm install`` trenger kun å kjøre ved første oppstart av programmet
```bash
    cd DirectoryServer
    npm install
```

<br/><br/>

## **Bruker Manual**
---

Programmet startes i denne rekkefølgen:
1. Directory Server
     ```bash
        cd DirectoryServer
        node index.js
    ```
2. Node
    - Anbefalt for å starte 3 instasner av dette programmet.
    - Dersom du får " Could not copy" error ved oppstart bruk:
        - ``dotnet run --no-build``
     ```bash
        cd Node
        dotnet run
    ```
3. Client
    - Start proxy serveren lokalt på port 3000
    ```bash
        cd Client
        dotnet run
    ```

4. Bruk av [SwitchyOmega](https://github.com/FelisCatus/SwitchyOmega) proxy for google chrome (**Valgfritt**  andre proxy servere kan brukes)
    - SwitchyOmega Proxy brukes for å koble Chrome nettleseren til den lokale proxy serveren. SwitchyOmega proxy er også anbefalt fordi den har funksjoner som gjør at brukeren kan spesifisere hvilken trafikk som blir omdirigert som blant annet HTTP forespørsler.

    - Installasjon av SwitchyOmega (Google Chrome).
        - [Last ned](https://chrome.google.com/webstore/detail/proxy-switchyomega/padekgcemlokbadohgkifijomclgjgif?hl=en) programmet fra Chrome Web Store.
        1. Velg "New Profile..." under menyen Profiles 
        2. Trykk på "Show Advanced" og sett "http://" protokollen til HTTP og skriv inn "127.0.0.1" i ``server`` feltet og deretter "3000" i ``port`` feltet.
        3. Trykk Apply Changes
        4. Trykk på Switchy Omega Ikonet som ligger i chrome og velg den nye profiler. Dette må gjøres hver gang chrome startes.

        ![Instalasjonsbilde som viser hvor man skal see når SwitchyOmega blir konfigurert](http://65.108.213.178:3000/api/image?imageID=speoUP2zoGfVNLWockTj)

<br/><br/>

## **Kjøring av tester**
---
- Directory Server:
    ```bash
        cd DirectoryServer
        npm run test        
    ```
- Gode nettsider for å teste at programmet fungerer i nettleseren:
    - http://eu.httpbin.org/
        - Store meldinger (1.4 mb jquery fil) 
        - Kan brukes for å teste mange http metoder.
    - http://datakom.no/
        - Simpel http nettside

<br/><br/>

## **Kryptografi**

SharpION nettverket fungerer ved at en bruker gjør et request til Client programmet. Client programmet vil så opprette en tunnel mellom tre noder. Tunnelen blir oppretter ved at Client og hver enkelt node lager en sesjonsnøkkel. Denne prosessen er som følger.- 
1. Client krypterer sin del av sesjonsnøkkelen ved hjelp av Noden sin offentlige nøkkel. 
1. Noden tar imot dataen og lager den andre delen av nøkkelen. Denne blir kryptert ved hjelp av RSA og Client sin offentlige nøkkel. Deretter blir den sendt tilbake til Client programmet.
1. Noden og Client har nå opprettet en sesjonsnøkkel og prosessen gjennomføres med alle noder i tunnelen. Der en sesjonsnøkkel har blitt opprettet brukes AES kryptering. Dette gjøres av klienten. Klienten krypterer (nodeNr - 1) ganger. Når en node får tilsendt data og dette ikke er en "key exchange" (RSA krypter). Vil den dekryptere sitt skall og sende daten videre.

Når tunnelen er opprettet vil dataen fra brukeren sendes igjennom tunnelen. Dette gjøres ved at klienten krypterer en gang per node i tunnelen ved hjelp av sesjonsnøkkelen generert tidligere. Deretter blir dataen send videre og hver node dekrypterer sitt skall. Dette gjøres til dataen har nådd målet. Når data sendes tilbake blir daten kryptert på hver node. Deretter dekrypterer Client programmet en gang for hver node ved hjelp av sesjonsnøkkelene. Når dette er gjort sendes den ukrypterte daten til brukeren.

Bildet under viser denne flyten beskrevet over:
![Bilde som viser kryptografi flyt](http://65.108.213.178:3000/api/image?imageID=eZUJgkH4xIGKwP7zV1Li)

<br/><br/>

## **Ekstern informasjon**
---
Ekstern informasjon brukt i dette prosjekter er som føgler:
- C# Dotnet dokumentasjon 
    - https://docs.microsoft.com/en-us/dotnet
- Node.js Express dokumentasjon 
    - https://expressjs.com/
- Node.js Testing guide
    - Brukt som hjelp for å skrive tester.
    - https://rahmanfadhil.com/test-express-with-supertest/
- C# JSON.net dokumentasjon 
    - https://www.newtonsoft.com/jsonschema/help/html/Introduction.html
- Informasjon om onion routing 
    - https://en.wikipedia.org/wiki/Onion_routing
    - https://www.geeksforgeeks.org/onion-routing/



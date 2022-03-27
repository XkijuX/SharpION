# SharpION

![SharpionLogo](http://65.108.213.178:3000/api/image?imageID=Njf1nF2r9VnrQWuRb8c3)
SharpION er et Onion Nettverk implementert i C#. SharpION fungerer som en tunnel mellom en Client og Serveren. Dette gjøres ved at brukeren kobbler seg på SharpION nettverket. Client en en inngang til SharpION nettverket og fungerer som en proxy. Denne proxyen kan brukes med nettlesere som blant annet [Google Chrome](https://www.google.com/chrome/) ved bruk av extensions. 

Når proxy serveren får et request vil en ny rute igjennom SharpION nettverket opprettes. Det velges tre noder fra nettverket. Deretter skapes sikre veier mellom disse nodene ved hjelp av kryptering. 

## Innhold:

1. [Funksjonalitet](#funksjonalitet)
1. [Fremtidig arbeid / mangler](#fremtidig-arbeid--mangler--svakheter)
1. [Eksterne avhengigheter](#eksterne-avhengigheter)
1. [Intallasjonsinstrukser](#intallasjonsinstrukser)
1. [Bruker Guide](#bruker-guide)
1. [Running Tests](#kjøring-av-tester)
1. [Ekstern infomasjon](#ekstern-informasjon)


## Funksjonalitet
- Sending av HTTP request
    - Støtter http metoder som GET, PUT, POST, DELETE
- Støtte for visuell fremvisning
    - Kan brukes igjennom chrome og andre nettlesere som støtter proxy for HTTP koblinger.
- Kryptografi
    - Bruker RSA og AES-CFB for å skjule meldingene i nettverket.
- DNS lookup
    - Henter ip addresser fra DNS servere fra HTTP header.

## Fremtidig arbeid / mangler / svakheter
SharpION er et program som ikke er ferdig utviklet. Programmet har fortsatt mangler og svakheter. Disse er som følger:
- 100% Test dekning (svakhet)
    - Viktig for å sikre at programmet og funksjoner fungerer.
- Garlic Routing (svakhet)
    - Selv om onion routing fungerer bra for å skjule data er det ikke perfekt. Ved hjelp av timing analyser er det mulig å finne avsender og destinasjon. En måte å løse dette på er å bruke Garlic Routing. Derfor kan det være aktuelt for videre arbeid.
- HTTPS (mangel)
    - SharpION støtter bare HTTP nå som ikke er sikkert på endenoden. Dersom en ondsindet person kjører en node som blir endenoden kan denne personen fange traffiken som går igjennom nettverket.
- Andre Nettverkspakker (mangel)
    - SharpION kan fungere som et VPN nettverk som støtter alle pakker som sendes fra maskiner.
- Støtte for ipaddresser (mangel)
    - SharpION støtter ikke urler i form av ip adresser.

## Eksterne avhengigheter
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
        - Modul for å testing av HTTP (brukes for å kjøre test forespørlser til express serveren)
- Node (Bruker ingen eksterne avhengigheter)

## Intallasjonsinstrukser

For å kjøre programmene kreves både dotnet og node.js.
- Installasjons linker:
    - [dotnet](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
    - [node.js](https://nodejs.org/en/download/)

For å installere dependencies for Client:
```bash
    cd Client
    dotnet restore
```

For å starte dependecies på Directory Server:
- ``npm install`` trenger kun å kjøre ved første oppstart av programmet
```bash
    cd DirectoryServer
    npm install
```

## Bruker Guide
Programmet startes i denne rekkefølgen:
1. Directory Server
     ```bash
        cd DirectoryServer
        dotnet run
    ```
2. Node
    - Anbefalt for å starte 3 instasner av dette programmet.
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
    - SwitchyOmega Proxy brukes for å koble Chrome nettleseren til den lokale proxy serveren. SwitchyOmega proxy er også annbefalt fordi den har funksjoner som gjør at brukeren kan spesifisere hvilken trafikk som blir omdirigert som blant annet HTTP forespørsler.

    - Installasjon av SwitchyOmega (Google Chrome).
        - [Last ned](https://chrome.google.com/webstore/detail/proxy-switchyomega/padekgcemlokbadohgkifijomclgjgif?hl=en) programmet fra Chrome Web Store.
        1. Velg "New Profile..." under menyen Profiles 
        2. Trykk på "Show Advanced" og sett "http://" protokollen til HTTP og skriv inn "127.0.0.1" i ``server`` feltet og deretter "3000" i ``port`` feltet.
        3. Trykk Apply Changes
        4. Trykk på Switchy Omega Ikonen som ligger i chrome og velg den nye profiler. Dette må gjøres hver gang chrome startes.

        ![Instalasjonsbilde som viser hvor man skal see når SwitchyOmega blir konfigurert](http://65.108.213.178:3000/api/image?imageID=speoUP2zoGfVNLWockTj)

## Kjøring av tester
- Directory Server:
    ```bash
        cd DirectoryServer
        npm run test        
    ```

## Ekstern informasjon
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



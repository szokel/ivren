Favicon

A realfavicongenerator.net oldahoz a bemenet:

	c:\repo\store\assets\favicon\favicon-lila-plain.png
	
amit ezzel allitottunk elo

	c:\DL\InkscapePortable\inkspace\InkscapePortable.exe

ebbol az inputbol:

	c:\repo\store\assets\favicon\favicon-lila-plain.svg

view_video.php?viewkey=ph5ea39d65d7fbf


# generalas

[Favicon Generator](https://realfavicongenerator.net)

1. feltoltjuk az inputfajlt, ami egy 512x512-es png legyen (amit pl. az Inkspace-szel allitunk elo SVG-bol).

> itt arra ugyeljunk, hogy a kep toltse ki a rendelkezesre allo teret (jelen esetben ez 512x512), ehhez ellenorizzuk le ugy, hogy megnyitjuk egy kepeditorban, es a corp funkcioval latni fogjuk, hogy a teljes kepmerethez viszonyitva mekkora a kep.

> ha elhagyjuk az ellenorzest, es kicsi a kep kitoltottsege, akkor hangyafasznyi 16x16-os icon lesz belole.

2. feltoltes utan gorgessuk az oldalt lefele, a macOS Safari reszig, es varjuk meg, mig a generalas itt is vegbemegy!

3. macOS Safari ha kesz, ill. az osszes tobbi is, akkor nyomjuk csak meg a Generate your Favicons and HTML code gombot.


4. atvalt a bongeszo az "Install your favicon" oldalra, ahol a tab-on az ASP.NET Core fület válasszuk!

5. várjuk meg itt is a generalas végét, majd az "1. Download your package: " mögötti Favicon package gombbal letölthetjük a csomagot.

6. a tobbi pontban ott vannak az utasítasok, a favicon beépítésérhez.




# Install your favicon - favicon_package_v0.16.zip

1. Download your package: favicon_package_v0.16.zip

2. Extract this package in <ASP.NET Core project>/wwwroot. If your site is http://www.example.com, you should be able to access a file named http://www.example.com/favicon.ico.

3. Create a partial named <ASP.NET Core project>/Views/Shared/_Favicons.cshtml and populate it with:

```
<link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png">
<link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png">
<link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png">
<link rel="manifest" href="/site.webmanifest">
<meta name="msapplication-TileColor" content="#da532c">
<meta name="theme-color" content="#ffffff">
```

4. Edit <ASP.NET Core project>/Views/Shared/_Layout.cshtml to include the partial you have just created in the <head> section. For example:

```
<!DOCTYPE html>
<html>
  <head>
    <!-- Other head elements -->
    @Html.Partial("_Favicons")
  </head>
  <body>
    <!-- Other body elements -->
  </body>
</html>
```

5. Optional - Once your web site is deployed, check your favicon.

6. Optional - Your favicon is fantastic. 


These instructions were originally written by [Andrew Lock](https://andrewlock.net/#open)



# mese

Van a Bookmarks bar-omon egy ilyen (nem tudom, mikor vettem fel): [Toptal | Web Developer Checklist](https://www.toptal.com/developers/webdevchecklist)

benne a Usability részben a Favicon, ahol [Real Favicon Generator](https://realfavicongenerator.net/)

Itt feltolthetunk egy kepet, es legeneralja hozza a faviconokat egy csomo platformhoz!

Aztan van itt egy [Favicon Generator for ASP.NET Core](https://realfavicongenerator.net/favicon/aspnet_core#.XriogGgzZaQ)


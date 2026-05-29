# Let's Encrypt SSL beállítása ASP.NET Core alkalmazáshoz Ubuntu szerveren -- letsencrypt Certbot tanúsítványt

Az alabbi leiras a ChatGPT fiokomban (szoke.laszlo@gmail.com) ebben a parbeszedben van: Asp.Net Core 8 Deploy

📌 Let's Encrypt SSL beállítása ASP.NET Core alkalmazáshoz Ubuntu szerveren
A Let's Encrypt egy ingyenes SSL/TLS tanúsítványszolgáltató, amely lehetővé teszi, hogy az ASP.NET Core alkalmazásod HTTPS-t használjon.
Ha a webalkalmazásod NGINX vagy Apache reverse proxy mögött fut, akkor az SSL tanúsítványt a proxyra kell telepíteni, nem közvetlenül az ASP.NET Core-ra.

## 1️⃣ Ellenőrizd, hogy az NGINX telepítve van-e

📌 Futtasd ezt a parancsot, hogy megnézd, telepítve van-e az NGINX:

```
nginx -v
```

Ha nincs telepítve, telepítsd:

```
sudo apt update
sudo apt install nginx -y
```

✅ Ez telepíti az NGINX-et, ha még nincs fent.

## 2️⃣ Állítsd be az NGINX-et reverse proxyként

Nyisd meg az NGINX konfigurációs fájlt:

```
sudo nano /etc/nginx/sites-available/myapp
```

📌 Másold be ezt a konfigurációt, és cseréld le a wover.hu-ot a saját domain nevedre:

```
server {
    listen 80;
    server_name wover.hu;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

📌 Mentés után aktiváld a konfigurációt:

```
sudo ln -s /etc/nginx/sites-available/myapp /etc/nginx/sites-enabled/
sudo systemctl restart nginx
```

✅ Ezzel az NGINX most már a wover.hu címre érkező HTTP kéréseket továbbítja az ASP.NET Core alkalmazásodra.

## 3️⃣ Telepítsd a Let's Encrypt tanúsítványt (Certbot)

📌 Telepítsd a certbot és a nginx plugint:

```
sudo apt install certbot python3-certbot-nginx -y
```

✅ Ez telepíti a szükséges eszközöket az SSL beállításához.

## 4️⃣ Generálj SSL tanúsítványt a Let's Encrypt-tel

📌 Futtasd a következő parancsot:

```
sudo certbot --nginx -d wover.hu -d www.wover.hu
```

🔹 Mit csinál ez?

- Lekér egy SSL tanúsítványt a Let's Encrypt-től a wover.hu és www.wover.hu domainekre.

- Automatikusan beállítja az NGINX konfigurációt a HTTPS használatára.

📌 Ha a folyamat sikeres, egy ilyen üzenetet fogsz látni:

```
Congratulations! Your certificate and chain have been saved at:
   /etc/letsencrypt/live/wover.hu/fullchain.pem
Your key file has been saved at:
   /etc/letsencrypt/live/wover.hu/privkey.pem
```

✅ Most az NGINX automatikusan átirányítja az összes HTTP forgalmat HTTPS-re!

## 5️⃣ Ellenőrizd az SSL működését

📌 Indítsd újra az NGINX-et:

```
sudo systemctl restart nginx
```

📌 Nyisd meg a böngésződet, és látogasd meg a weboldalad:

```
https://wover.hu
```

✅ Ha mindent jól csináltál, a böngésző zöld lakatot fog mutatni a címsorban! 🔒

## 6️⃣ Automatikus SSL megújítás beállítása

A Let's Encrypt tanúsítványok 90 napig érvényesek, de automatikusan megújíthatók.

📌 Teszteld a megújítást:

```
sudo certbot renew --dry-run
```

📌 Ha nincs hiba, állíts be egy automatikus megújítást a cron segítségével:

```
sudo crontab -e
```

📌 Adj hozzá egy új sort a fájl végére:

```
0 3 * * * certbot renew --quiet
```

✅ Ez minden nap hajnali 3-kor megpróbálja megújítani az SSL tanúsítványt.

## 🚀 Összegzés

| Lépés | Parancs |
|--|--|
| NGINX telepítése | sudo apt install nginx -y |
| Reverse proxy beállítása | sudo nano /etc/nginx/sites-available/myapp |
| Certbot telepítése | sudo apt install certbot python3-certbot-nginx -y |
| SSL tanúsítvány generálása | sudo certbot --nginx -d wover.hu -d www.wover.hu |
| SSL megújítás tesztelése | sudo certbot renew --dry-run |
| Automatikus megújítás beállítása | sudo crontab -e |
|--|--|

✅ Most már a weboldalad biztonságosan működik HTTPS-en! 🔒🚀

## 🚀 Következő lépések

1️⃣ Próbáld ki a certbot telepítését és futtasd a --dry-run tesztet!

2️⃣ Ha hiba van, másold ide az üzenetet, és segítek megoldani! 😊


# Etc

```
l@wover:~$ sudo certbot --nginx -d wover.hu -d www.wover.hu
Saving debug log to /var/log/letsencrypt/letsencrypt.log
Requesting a certificate for wover.hu and www.wover.hu

Successfully received certificate.
Certificate is saved at: /etc/letsencrypt/live/wover.hu/fullchain.pem
Key is saved at:         /etc/letsencrypt/live/wover.hu/privkey.pem
This certificate expires on 2025-06-21.
These files will be updated when the certificate renews.
Certbot has set up a scheduled task to automatically renew this certificate in the background.

Deploying certificate
Successfully deployed certificate for wover.hu to /etc/nginx/sites-enabled/wover
Successfully deployed certificate for www.wover.hu to /etc/nginx/sites-enabled/wover
Congratulations! You have successfully enabled HTTPS on https://wover.hu and https://www.wover.hu

- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
If you like Certbot, please consider supporting our work by:
 * Donating to ISRG / Let's Encrypt:   https://letsencrypt.org/donate
 * Donating to EFF:                    https://eff.org/donate-le
- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
l@wover:~$ sudo mc

l@wover:~$
```
_

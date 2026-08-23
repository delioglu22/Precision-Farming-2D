---
name: artist
description: Bu oyunun görsel dilini koruyarak art üret ve art kararları ver. Yeni doku, tile, renk, palet, dekor ya da import ayarı gerektiren her işte MUTLAKA bu skill'i kullan — "art" kelimesi geçmese bile. Bir rengin ayarlanması, bir görselin üretilmesi, bir sprite'ın projeye alınması bu kapsamdadır. Kullanıcı /artist yazdığında da bu şapka takılır.
---

# Artist şapkası

Bu oyunun bir görsel dili var ve o dil piksel düzeyinde düzenli. Senin işin yeni
bir şey eklerken o düzeni bozmamak.

**Önce `docs/art.md`'yi oku.** Izgara, ışık yönü, palet ve tile kuralı orada.
Buradaki kurallar o dökümanı tekrar etmez, onu nasıl uygulayacağını söyler.
Sayılar değişirse `art.md` değişir, bu dosya değişmez.

## Üretmeden önce ölç

Buradaki art elle çizilmiş gibi durur ama değildir — piksele kadar düzenlidir.
Yanına bir şey koyacaksan önce mevcudu ölç.

Somut: altı ekinin paleti, çizgi periyodunun 16 piksel olduğu ve çizgilerin hangi
izometrik eksen boyunca gittiği, mevcut PNG'ler taranarak bulundu. Göz kararı
seçilseydi **"neredeyse uyan"** bir şey çıkardı — ki bu, belirgin şekilde farklı
olmaktan kötüdür, çünkü hata gözü rahatsız eder ama nedenini söylemez.

Ölçmek pahalı değil: dosyayı `Texture2D.LoadImage` ile oku, piksellere bak.

## Düz renk dosya değil, kutudur

Tek renkten ibaret bir yüzey **texture olmaz**. Katmanın Inspector'daki renk
alanına yazılır. Sadece bir **desen** kendi dosyasını hak eder.

Somut: pahın, konturun ve iki yan yüzün rengi 8x8 tek renk PNG'lere gömülmüştü.
Rengi değiştirmek dosya yeniden üretmek demekti. Dördü tek beyaz dokuya indi,
renkler `SpriteShapeRenderer.color`'a taşındı — dört dosya azaldı ve renk artık
tıklanabilir bir kutu. Bu hatayı kullanıcı yakaladı, sen yakala.

## Tekrar eden yüzey dolgudur, nesne yığını değil

Bir yüzey aynı şeyi tekrar tekrar kaplıyorsa cevabı tile'lanan bir dolgudur. Dolgu
parselle birlikte bedavaya büyür; nesneler büyümez, onları üretmen gerekir.

Somut: orman için tek tek ağaç sprite'ları üretilip footprint'e göre serpen bir
script yazıldı. Çalıştı — ama ekinlerde aynı problem zaten `fill_wheat` ile
çözülmüştü. `fill_wood` tek dosya, script yok, sahnede 470 nesne yok, ve
ölçekleme bedava. Script ve üç ağaç sprite'ı tamamen silindi.

Gerçek nesne sadece **tek tek anlamlı** olan şeyler için: oyuncunun tıklayacağı,
sayacağı ya da yerinden oynatacağı bir şey. Manzara dolgudur.

## Işık yönü pazarlık konusu değil

Sağ üstten, sabit. Yeni her katı nesnede üst yüz aydınlık, sol alta düşen yüz
sahnenin en koyusu, sağ alta düşen yüz ikisinin arası. Buna uymayan bir nesne
tarlaların yanında yamalı durur — tek başına bakınca güzel görünse bile.

## Görsel üreticiden kesin geometri isteme

`generate_image` doku ve dekor için iyidir: toprak yüzeyi, ağaç, kaya, su. Izgaraya
oturması gereken hiçbir şey için değil. İzometrik bir tile üç piksel şaşarsa
haritanın tamamında saç teli kalınlığında dikiş çıkar, ve bu tek karede görünmez —
yirmi tane yan yana gelince görünür.

Şeklin matematiği varsa hesapla. Çizdirip sonra hizalamak, hesaplamaktan uzundur.

## Import ayarı art'ın parçasıdır

Yanlış import, yanlış çizilmiş dosya kadar bozar ve bulması daha zordur.

| Ne | Ayar |
| --- | --- |
| SpriteShape dolgu dokusu | **Default** tip, **Repeat** wrap — Sprite tipi verirsen şekil sessizce hiç çizilmez |
| Sprite | PPU **100**, pivot bilinçli seçilmiş |
| Hepsi | Point filter, mipmap kapalı, sıkıştırma yok |

Düz renk ve keskin kenarlı art'ta sıkıştırma bantlaşma yapar; bu paletlerde
fark edilir.

## Ters tarafa savrulma

Bu skill "hiç üretme, hep ölç" değil. Dekor, doku, palet genişletme — bunlarda
serbestsin ve cesur olman iyidir. Kısıt yalnızca **ızgaraya oturmak zorunda olan**
şeyler için geçerli. Bir ağaca kimse cetvelle bakmaz; bir tarla tile'ına bakar.

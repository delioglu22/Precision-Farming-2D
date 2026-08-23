---
name: hand-it-over
description: Bu Unity projesinde işin bir parçasını kullanıcıya devret; o Unity'de kendi eliyle yapsın, sen bekle ve sonra kontrol et. "[SENİN SIRAN]" bileti geldiğinde MUTLAKA bu skill'i kullan. Kullanıcı "bunu ben yapayım", "bana bırak", "nasıl yapılır" dediğinde de bu skill geçerlidir.
---

# İşin bir parçasını kullanıcıya devret

Kullanıcı bu oyunu kendi yapıyor; sen yardım ediyorsun. Her şeyi senin yapman
kısa vadede hızlı, uzun vadede kullanıcının Unity'yi unutması demek. Bunu
kendisi söyledi. Bu yüzden arada bir işin bir parçası ona gider.

Devir gerçek bir parça olmalı. "Sen de bir kere dene" değil — o parçayı
sen yapmayacaksın, kullanıcı yapacak, iş onun elinden çıkacak.

## Hangi parça devredilir

İyi bir devir parçası şu dördünü birden tutar:

1. **Unity'nin arayüzünde yapılır.** Inspector, Animation penceresi, Hierarchy,
   Sprite Editor, Scene view. Kullanıcının kaybetmekten korktuğu el bu.
2. **İçinde bir karar var.** Sadece tıklama değil; "hangi değer", "hangi
   sıra", "hangi anchor" diye düşündüren bir şey.
3. **5–15 dakika sürer.** Daha kısası ders değil, daha uzunu angarya.
4. **Geri alınabilir.** Yanlış yaparsa tek Ctrl+Z ya da tek alan düzeltmesiyle
   dönülür.

Bu projede işe yarayanlar:

| Devir | İçindeki karar |
| --- | --- |
| Inspector'daki ayarları oturtmak (`focusDamping`, `edgeMargin`, `sheetCover`, seçili ton) | Hangi değer doğru hissettiriyor — bunu sadece oynayan bilir |
| Yeni bir parsel yerleştirmek | `sortingOrder`'ı topolojik kurala göre bulmak |
| Animation penceresinde yeni bir poz klibi | Hangi özellik sürülür, hangisi instance override'ı ezer |
| Prefab'a bileşen ekleyip referansı bağlamak | Prefab'ta mı instance'ta mı olmalı |
| Bir parselin footprint'ini ve ekinini oturtmak | Hangi boy ve hangi ekin komşularının yanında doğru duruyor |
| Bir katmanın rengini palete oturtmak | Işık yönüne uyuyor mu, mevcut dört tonun neresine düşüyor |
| Bir UI öğesini anchor + layout group ile yerleştirmek | Kodla değil yerleşimle çözmek |
| Yeni bir `ScriptableObject` varlığı açıp iki sahneye bağlamak | Sahneler arası referansın neden asset olması gerektiği |

Devretme:

- Yarım kalmış bir refactor'ün ortasını
- Kurulumu 6 MCP çağrısı isteyen şeyi
- Sadece yazı işini (bir dosyayı baştan yazmak öğretmez)
- Sen de nasıl yapılacağını bilmediğin şeyi
- Kullanıcının o an acelesi olduğunu söylediği işi

## Nasıl devredilir

Dört satır. Tıkla-tıkla tarifi **verme** — tarifi izlemek öğretmez, o yüzden
nerede olduğunu ve neyin doğru olduğunu söyle, sırayı kullanıcı bulsun.

```
Bu parçayı sen yap:

  Ne     Parcel 7'yi haritanın kuzeydoğusuna ekle.
  Nerede SampleScene > World > Parcels, Parcel.prefab'tan bir instance.
  Neden  sortingOrder elle bulunacak: A, B'den önce çizilir — B bir izometrik
         eksende daha ileride ve diğerinde üst üste biniyorlarsa.
  Kontrol Tıklayınca doğru parsel seçiliyorsa collider da doğru oturmuştur.

Bittiğinde söyle, konsolu ve sahneyi kontrol ederim.
```

Sıkışırsa yardım et — ama önce bir kez kendi denesin.

## Devir açıkken ne yaparsın

**O parçayı sen yapma.** Beklerken boş durma: aynı işin devre bağlı olmayan
kısımlarını bitir, script tarafını hazırla, ama devredilen parçaya dokunma.

Kullanıcı "sen yap" derse tartışma, yap ve bileti kapat.

## Bittiğinde

1. **Kontrol et.** `refresh_unity` + `read_console` `types: ["error"]` → 0
   kayıt. Sahne değiştiyse durumu `execute_code` ile oku, gerekiyorsa
   ekran görüntüsü al.
2. **Tek satır geri bildirim.** Doğruysa doğru de. Yanlışsa neyin yanlış
   olduğunu ve nedenini söyle — düzeltmeyi yine kullanıcı yapsın.
3. **Bileti kapat:** `node .claude/hooks/your-turn.js --close`
4. Normal işe devam et.

## Bilet mekanizması

`.claude/hooks/your-turn.js` bir `UserPromptSubmit` hook'u. Her N commit'te
(varsayılan 2) bir "[SENİN SIRAN]" bileti düşürür — yani her N iş parçasında
bir. Karar bu skill'de, sayaç hook'ta; hook hangi işin devredilebilir olduğunu
bilemez, o yüzden seçim sana ait.

Bilet açık kalırsa hook 3 kez hatırlatır, sonra düşürür. Yani biletle
ilgilenmemek bir seçenek değil: ya devret, ya **neden devretmediğini tek
cümleyle söyle** ve bileti kapat.

Kullanıcının kontrolleri:

| | |
| --- | --- |
| `#benim-sıram` | istemin içine yazarsa hemen bilet çıkar |
| `#kendin-yap` | bu seferlik atla |
| `node .claude/hooks/your-turn.js --status` | durum |
| `--every <n>` / `--off` / `--on` | sıklık ve açma kapama |

## Ters tarafa savrulma

Bu skill "işi kullanıcıya yıkma" değil. Kullanıcı bir şey istediyse onu
yapıyorsun; devir işin **bir parçası**, tamamı değil. Bilet geldi diye
kullanıcıya iki saatlik iş verme, ve devir yüzünden işi yarım bırakma —
devredilen parça dışındaki her şey yine senin.

Uygun parça yoksa uydurma. "Bu işte devredilecek anlamlı bir parça yok, çünkü
..." de, bileti kapat, devam et.

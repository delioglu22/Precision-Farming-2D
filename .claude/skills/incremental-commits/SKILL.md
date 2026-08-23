---
name: incremental-commits
description: Bu Unity projesinde işi küçük parçalara bölerek ilerle ve her çalışan parçadan sonra commit at. Kullanıcı bir özellik, ekran, sistem ya da düzeltme istediğinde MUTLAKA bu skill'i kullan — "commit" kelimesi geçmese bile. Birden fazla dosyaya dokunan, birden fazla adımı olan ya da tamamlanması birkaç dakikadan uzun sürecek her iş bu kapsamdadır. Sadece tek satırlık düzeltmelerde atlanabilir.
---

# Küçük parçalar hâlinde commit

Bu projede tek bir dev commit yoktur. İş küçük, çalışan parçalara bölünür ve
her parça ayrı commit edilir.

Sebebi: bir şey bozulduğunda kullanıcının geri dönebileceği bir nokta olmalı.
Tek büyük commit, "çalışıyordu, sonra çalışmamaya başladı" durumunda hiçbir
işe yaramaz.

## Çalışma şekli

1. **Önce planı çıkar.** İşi 2–6 parçaya böl. Her parça tek başına derlenip
   çalışan bir durum bırakmalı. Planı kullanıcıya kısaca göster, sonra başla.
2. **Bir parçayı bitir.** Sadece o parçaya ait değişiklikleri yap.
3. **Derlemeyi doğrula.** Unity MCP ile konsolu oku. Derleme hatası veya
   konsolda hata varsa commit ATMA, önce düzelt.
   **Konsolu okuyamıyorsan commit ATMA.** Kullanıcıya durumu söyle ve bekle.
   Tahminle "herhalde derleniyordur" deme.
4. **Onay iste, kendi başına commit atma.** Ne yaptığını tek cümleyle özetle,
   commit mesajını yaz ve kullanıcıya göster. Sonra dur.
   Kullanıcı Unity'de deneyip onay verene kadar bekle. Onay gelince commit et.
   Kullanıcı sorun bildirirse önce düzelt, sonra tekrar onay iste.
5. **Sonraki parçaya geç.** Bütün parçalar bitene kadar tekrarla.
6. **Sonunda özetle.** Hangi commit'lerin atıldığını tek satırlık liste
   hâlinde kullanıcıya söyle.

Derleme kontrolü senin işin, oynanış kontrolü kullanıcının işi. Sen oyunu
oynayamazsın; "iyi çalışıyor" diye varsayma.

## Bir parça ne kadar büyük olmalı

Doğru boyut: tek bir cümleyle anlatılabilen, çalışır bir değişiklik.

İyi parçalar:
- "Parsel verilerini tutan veri sınıfı ve varsayılan değerler"
- "Haritanın yatay kaydırılması"
- "Parsel seçimi ve seçili görünüm"
- "Alt panelin açılıp kapanması"

Kötü parçalar:
- "Ana ekranı yap" (çok büyük — yukarıdaki dörde bölünür)
- "Değişken adını düzelt" (çok küçük — bir sonraki parçayla birlikte gider)

## Riskli değişiklikten önce commit at

Büyük bir yeniden düzenleme, mimari değişiklik ya da çok dosyaya dokunan bir
işlemden ÖNCE mevcut çalışan durumu commit et. Değişiklik kötü giderse
kullanıcı geri dönebilsin.

Bu commit için onay beklemene gerek yok — yeni bir şey eklenmiyor, sadece
zaten çalışan durum kaydediliyor.

## Asla commit etme

- Derlenmeyen kod
- Unity konsolunda hata bırakan kod
- Derleme durumunu doğrulayamadığın kod
- Kullanıcının onaylamadığı bir parça
- `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` klasörleri
  — bunlar `.gitignore`'da olmalı. Değilse önce `.gitignore`'u düzelt, sonra
  devam et.
- Birbiriyle ilgisiz değişiklikleri aynı commit'te

## Commit mesajı

İngilizce, kısa, emir kipi. Bu proje kullanıcının portfolyosunda yer alacak,
mesajlar okunabilir olsun.

Biçim: `<tip>: <ne yapıldı>`

Tipler: `feat` (yeni özellik), `fix` (hata düzeltme), `refactor` (davranış
değişmeden yeniden düzenleme), `chore` (yapılandırma, gitignore, paket)

```
feat: add horizontal map panning with clamped bounds
feat: show parcel info panel on selection
fix: charge bar not updating during drag
chore: add Unity gitignore
```

Gövde yazma, tek satır yeterli. Mesaj 72 karakteri geçmesin.

Mesaja `Co-Authored-By` satırı, "Generated with Claude Code" satırı ya da
başka herhangi bir araç imzası **ekleme**. Sadece tek satırlık mesaj.

Mesaj kodun son hâlini anlatsın; oturum sırasındaki deneme yanılmayı anlatma.
"Önce şunu denedim, olmadı, sonra bunu yaptım" gibi bir şey mesaja girmez.

## Unity'ye özel

- `.meta` dosyalarını da commit et — Unity onlar olmadan referansları kaybeder
- Sahne dosyası (`.unity`) değiştiyse ilgili commit'e dahil et
- Prefab (`.prefab`) değişiklikleri de `.meta` dosyasıyla birlikte gider
- ScriptableObject varlıkları hem `.asset` hem `.meta` dosyasıyla birlikte
  commit edilmeli

## Kullanıcıya ne söylenir

Her parçadan sonra iki satır: ne yapıldığı ve önerilen commit mesajı. Sonra
onay bekle. Uzun açıklama yazma; kullanıcı ilerlemeyi görmek istiyor, rapor
değil.

```
Parsel seçimi eklendi — parsele dokununca çerçevesi vurgulanıyor.
Önerilen commit: feat: show parcel info panel on selection
Unity'de deneyip onaylar mısın?
```

İş bitince kısa bir liste ver:

```
3 commit atıldı:
  feat: add parcel data model
  feat: add horizontal map panning
  feat: show parcel info panel on selection
```
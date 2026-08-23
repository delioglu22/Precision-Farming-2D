---
name: engine-first
description: Unity'de yeni bir davranış eklerken önce motorun hazır bileşenine bak, script'i son çare olarak yaz. Yeni bir MonoBehaviour gerektiren, girdi/tıklama işleyen, animasyon oynatan, UI gösterip gizleyen ya da veri taşıyan her işte MUTLAKA bu skill'i kullan — "script yazma" denmese bile. 50 satırı geçecek her yeni script bu kapsamdadır.
---

# Önce motoru kullan

Unity, küçük bir oyunun ihtiyacı olan şeylerin çoğunu zaten yapıyor. Kod yazmadan
önce o hazır çözümü bulmak senin işin.

## Kural

Yeni bir MonoBehaviour yazmadan önce **bu işi yapan Unity bileşeninin adını
söyle**. Varsa onu kullan. Yoksa neden olmadığını tek cümleyle yaz, sonra
script'i yaz.

50 satırı geçecek her yeni script için bu zorunlu. Kullanıcıya sunarken de
söyle: "şunu kullandım" ya da "Unity'de karşılığı yok çünkü ...".

## Bu teorik bir kural değil

Bu projede tıklama algılamak için 138 satırlık bir `ParcelSelector` yazıldı:
parmak takibi, sürükleme eşiği, `Physics2D.OverlapPointAll`, öndeki objeyi
seçme, ve "UI'a mı denk geldi" kontrolü. Kameradaki `Physics2DRaycaster` artı
`IPointerClickHandler` beşini de yapıyor. Script tamamen silindi, hiçbir şey
kaybolmadı. 440 satır 286'ya indi.

## Nereye bakılır

| İş | Hazır çözüm |
| --- | --- |
| Dünyadaki objeye tıklama | Kamerada `Physics2DRaycaster` + `IPointerClickHandler` |
| Sürüklerken tıklama sayılmaması | Zaten var — `EventSystem.pixelDragThreshold` |
| Tıklamanın UI'dan geçmemesi | Zaten var — EventSystem yutuyor |
| UI açma, kapama, kaydırma | `Animator`: bir bool, iki poz, aradaki geçiş |
| Ayarlanabilir değerler | `[SerializeField]` ve Inspector, sabit değil |
| Sahneler arası veri | İki sahnenin de baktığı bir `ScriptableObject` |
| UI yerleşimi | Anchor ve layout group, pozisyon hesaplayan kod değil |
| Zamanlama, sıralı olaylar | `Animator` ya da Timeline |
| Birden çok renderer'ın tek parça gibi sıralanması | `SortingGroup` — nesneye tek sayı, içine 0..n |

`SortingGroup` satırı da teoriden gelmiyor: parsel beş katmanlı bir levhaya dönüşünce "her parselin
sırasını katman başına beşe yay" diye bir plan yapılmıştı. `SortingGroup` katmanları bir arada tutup
parsele tek sayı bıraktığı için o plan tamamen düştü ve `Parcel.cs` kaç katman olduğunu bilmekten
kurtuldu.

## Ters tarafa savrulma

Kural **"script yazma" değil**, "alternatifini adıyla söyle". Bazı şeylerin
Unity'de karşılığı yok. `MapPan` bu yüzden var — hazır harita kaydırma bileşeni
yok, yazmak doğruydu. Gerekli kodu yazmaktan kaçınmak da en az gereksiz kod
yazmak kadar kötü.

## Asıl tehlike

Sorun API'yi bilmemek değil. Kendi yazdığın kodu programatik olarak doğrulamak
daha kolay olduğu için ona kayıyorsun. Buna direnip motorun davranışını
doğrula — EventSystem'e ışın attır, Animator'ı elle ilerlet, sonucu ölç.

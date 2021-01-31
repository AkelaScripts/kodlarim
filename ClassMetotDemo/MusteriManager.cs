using System;
using System.Collections.Generic;
using System.Text;

namespace ClassMetotDemo
{
    class MusteriManager
    {
        public void MusteriEkle(Musteri musteri)
        {
            Console.WriteLine("Tebrikler " + musteri.Ad + " isimli müşteri başarıyla eklendi!");
        }
        public void MusteriSil(Musteri musteri)
        {
            Console.WriteLine("Tebrikler " + musteri.Ad + " isimli müşteri başarıyla silindi!");
        }
        public void MusteriListele(Musteri musteri)
        {
            Console.WriteLine(musteri.Ad + " isimli müşterinin bilgileri aşağıda listelendi!");
            Console.WriteLine("Ad : " + musteri.Ad);
            Console.WriteLine("Soyad : " + musteri.Soyad);
            Console.WriteLine("E-Posta : " + musteri.Eposta);
            Console.WriteLine("Şifre : " + musteri.Sifre);
            Console.WriteLine("Yaş : " + musteri.Yas);
        }
    }
}

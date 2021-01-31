using System;

namespace ClassMetotDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Musteri musteri1 = new Musteri();
            musteri1.Ad = "Mehmet";
            musteri1.Soyad = "Aslan";
            musteri1.Eposta = "maslan@gmail.com";
            musteri1.Sifre = 1234;
            musteri1.Yas = 27;

            Musteri musteri2 = new Musteri();
            musteri2.Ad = "Ahmet";
            musteri2.Soyad = "Yılmaz";
            musteri2.Eposta = "ayilmaz@gmail.com";
            musteri2.Sifre = 1023;
            musteri2.Yas = 35;

            Musteri musteri3 = new Musteri();
            musteri3.Ad = "Samet";
            musteri3.Soyad = "Kaplan";
            musteri3.Eposta = "skaplan@gmail.com";
            musteri3.Sifre = 0152;
            musteri3.Yas = 42;

            Musteri[] musteriler = new Musteri[]
            {
                musteri1,
                musteri2,
                musteri3
            };

            MusteriManager musteriM = new MusteriManager();
            Console.WriteLine("Bankamıza Hoşgeldiniz!");
            Console.WriteLine( );
            musteriM.MusteriEkle(musteri1);
            Console.WriteLine( );
            musteriM.MusteriSil(musteri2);
            Console.WriteLine( );
            musteriM.MusteriListele(musteri3);
        }
    }
}

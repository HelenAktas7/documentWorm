@'
# 🪱 DocumentWorm (Kurumsal Doküman Kurdu)

> **Kurumsal dokümanlar üzerinde akıllı anlamsal arama (Semantic Search) ve halüsinasyonsuz soru-cevap üreten kurumsal düzeyde bir RAG (Retrieval-Augmented Generation) Backend Servisi.**

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16%20%2B%20pgvector-blue.svg)](https://github.com/pgvector/pgvector)
[![Google Gemini](https://img.shields.io/badge/Google%20Gemini-Flash%20%26%20Embedding-orange.svg)](https://ai.google.dev/)
[![Scalar](https://img.shields.io/badge/API%20Docs-Scalar-green.svg)](https://scalar.com/)

---

## 📌 Projenin Amacı ve Özeti

**DocumentWorm**, şirketlerin ve kurumların sahip olduğu PDF, teknik şartname, analiz ve dokümantasyon yığınlarını yapay zeka hafızasına alan ve kullanıcıların bu belgeler üzerinden anlık soru-cevap yapabilmesini sağlayan bir backend platformudur.

Sistem klasik anahtar kelime araması yerine **Vektörel Anlamsal Benzerlik (Vector Cosine Similarity)** kullanır; sorunun anlamını kavrar ve belgeden en alakalı bölümleri sayfa numaralarıyla birlikte alıntılayarak (**Citation**) doğrulanabilir yanıtlar üretir.

---

## 🚀 Temel Özellikler

- **⚡ .NET 9 Minimal APIs & Clean Architecture:** Yüksek performanslı, temiz katmanlı (Core, Infrastructure, Api) mimari.
- **🐘 PostgreSQL + pgvector & HNSW İndeksleme:** 768 boyutlu vektör uzayı üzerinde milisaniyeler seviyesinde kosinüs benzerliği araması.
- **🤖 Google Gemini AI Entegrasyonu:**
  - Vektörel Gömmeler (Embeddings): `gemini-embedding-001` (768-boyutlu normalize vektörler).
  - RAG Mantıksal Yanıt: `gemini-3.8-flash` (halüsinasyonu engelleyen sıkı sistem talimatları).
- **📄 Akıllı Doküman İşleme (Chunking & Parsing):**
  - `PdfPig` kütüphanesi ile sayfa bazlı metin çıkarma.
  - Kayan Pencere (Sliding Window) algoritması ile bağlam kaybını önleyen örtüşmeli (overlap) parçalama.
- **🛡️ Kurumsal Güvenlik & API Key Yönetimi:**
  - `X-API-KEY` başlığı ile özel istemci doğrulama.
  - Veritabanında **SHA-256** ile tuzlanmış hash'li saklama (`dw_live_...` formatı).
  - İstemci bazlı **Rate Limiting** (Dakikada 10 istek sınırı).
- **📖 Etkileşimli Scalar API Dokümantasyonu:** Swagger yerine modern, karanlık mod destekli interaktif test arayüzü (`/scalar/v1`).

---

## 🏗️ Mimari Yapı

```text
DocumentWorm/
├── src/
│   ├── DocumentWorm.Core/             # Çekirdek Varlıklar, DTO'lar, Arayüz Sözleşmeleri
│   │   ├── Entities/                  # Document, DocumentChunk, ApiClient
│   │   ├── DTOs/                      # Request / Response modelleri
│   │   └── Interfaces/                # IDocumentParser, IVectorStore, IRagService, vb.
│   │
│   ├── DocumentWorm.Infrastructure/   # Veritabanı, AI Servisleri, Parser'lar
│   │   ├── Data/                      # DbContext, pgvector HNSW konfigürasyonu, VectorStore
│   │   ├── Gemini/                    # GeminiEmbeddingService (Auto-retry), RagService
│   │   ├── Parsing/                   # PdfDocumentParser, SlidingWindowTextChunker
│   │   └── Auth/                      # ApiKeyValidator
│   │
│   └── DocumentWorm.Api/              # Minimal API Uç Noktaları, Middleware, Program.cs
│       ├── Endpoints/                 # Documents, Chat, Admin, Health
│       └── Middleware/                # ApiKeyEndpointFilter, RateLimiting
│
└── docker-compose.yml                 # pgvector özellikli PostgreSQL konteyneri

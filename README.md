# UWB.ANPR

Module kết nối camera ANPR (Automatic Number Plate Recognition) cho hệ thống UWB.
Tập trung vào **tầng kết nối thiết bị** với nhiều loại kênh nhận sự kiện.

> Tầng database **không** nằm trong repo này. Module DB hiện hữu chỉ cần implement
> 4 interface trong `UWB.ANPR.Abstractions/Ports/`.

## Kiến trúc

```
UWB.ANPR.Api ──▶ UWB.ANPR.Ingestion ──▶ UWB.ANPR.Hikvision ──▶ UWB.ANPR.Abstractions
```

`Abstractions` không phụ thuộc project nào, chứa domain type và các port.

| Project | Trách nhiệm |
|---|---|
| `UWB.ANPR.Abstractions` | Domain type (`CameraId`, `RawCameraEvent`, `CameraDescriptor`) + port cho DB |
| `UWB.ANPR.Hikvision` | ISAPI client, HTTP Digest per-camera, resilience/pooling, đọc multipart |
| `UWB.ANPR.Ingestion` | Pipeline persist-before-ack, 3 event source, supervisor, worker, metrics |
| `UWB.ANPR.Api` | Endpoint webhook, health check, DI wiring, placeholder để chạy thử |

## Ba loại kết nối

Tất cả đi qua cùng một abstraction `IAnprEventSource` và đổ về `IEventIngestPipeline`.

| Loại | Cơ chế | Đặc thù |
|---|---|---|
| **Webhook** | Camera POST tới hệ thống | Source thụ động; endpoint đẩy dữ liệu vào pipeline |
| **AlarmStream** | Hệ thống mở HTTP dài hạn | Tự reconnect, backoff lũy thừa + jitter, reset khi stream khỏe |
| **Polling** | Định kỳ gọi search API | Watermark + `SafetyLag` + `MaxWindow` |

Đăng ký bằng keyed DI, nên thêm loại kết nối mới chỉ cần một dòng:

```csharp
services.AddKeyedSingleton<IAnprEventSource, PollingEventSource>(AnprEventSourceKind.Polling);
```

`CameraSourceSupervisor` chạy đúng một source cho mỗi camera đang bật và tự khởi động lại khi crash.

## Bất biến quan trọng: persist-before-ack

Mọi source đều gọi `IEventIngestPipeline.IngestAsync`, nơi thứ tự được đảm bảo:
**ghi bền vững trước → đưa vào hàng đợi sau**.

| `IngestOutcome` | Ý nghĩa | HTTP webhook |
|---|---|---|
| `Accepted` | Đã ghi bền vững + đã vào hàng đợi | `200` |
| `Duplicate` | Cặp `(CameraId, EventId)` đã tồn tại | `200` |
| `PersistedButQueueFull` | Đã ghi bền vững, hàng đợi đầy → Recovery Job xử lý | `200` |
| `Discarded` | Payload sai định dạng nhưng raw bytes đã lưu | `200` |
| `PersistFailed` | **Chưa** ghi được → không ack thành công | `503` |

## Chạy thử

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/UWB.ANPR.Api
```

Gửi thử một sự kiện webhook:

```bash
curl -k -X POST https://localhost:5001/api/anpr/events/replace-with-random-key \
  -H "X-Uwb-Anpr-Secret: <secret>" \
  -H "Content-Type: application/xml" \
  --data-binary @sample-event.xml -i
```

Cấu hình camera trong `appsettings.json` (mục `Anpr:Cameras`), secret đặt qua
`dotnet user-secrets` hoặc biến môi trường — **không** commit vào repo.

## Việc cần làm trước khi dùng production

| Thành phần | Vị trí | Ghi chú |
|---|---|---|
| `IRawEventStore` | `Abstractions/Ports` | **Bắt buộc** unique index `(CameraId, EventId)` |
| `IWatermarkStore` | `Abstractions/Ports` | Bảng `(CameraId, Purpose, Value)` |
| `ICameraRegistry` | `Abstractions/Ports` | Đọc cấu hình camera + map webhook key |
| `ICameraCredentialResolver` | `Abstractions/Ports` | Giải mã credential, không ghi log |
| `IHikvisionPayloadParser` | `Hikvision` | **Cần payload mẫu thật** theo model/firmware |
| `MultipartEventReader.TryExtractPart` | `Hikvision` | Hoàn thiện khi có mẫu alarm stream thật |
| `IRecognitionProcessor` | `Ingestion` | Chuẩn hóa biển số + đối sánh + ghi kết quả |

Các class trong `Api/Placeholders/` chỉ để chạy thử (lưu trong bộ nhớ) — thay hết trước khi lên production.

## Những cạm bẫy đã xử lý sẵn

1. **Circuit breaker gộp camera.** `AddStandardResilienceHandler` dùng trạng thái breaker chung cho cả
   named client nên một camera chết sẽ chặn tất cả. Ở đây pipeline được phân vùng theo
   `ResiliencePipelineRegistry<string>` với key `hikvision:{CameraId}`.
2. **Digest auth với nhiều credential.** Không gán được `Credentials` trên handler dùng chung;
   `CameraId` được truyền qua `HttpRequestMessage.Options` và digest response tự tính, có buffer
   body để retry được request có content.
3. **Retry lỗi xác thực.** `CameraAuthenticationException` không nằm trong tập lỗi được retry, và
   alarm stream dừng hẳn khi gặp 401/403 — tránh làm camera lock account.
4. **`HttpClient.Timeout` giết stream dài hạn.** Alarm stream đặt `Timeout.InfiniteTimeSpan` và dùng
   read timeout riêng.
5. **Watermark dịch sớm.** `PollingEventSource` chỉ dịch watermark sau khi toàn bộ sự kiện đã ghi
   bền vững; gặp lỗi thì giữ mốc cũ để lần sau lấy lại.
6. **Hàng đợi không giới hạn.** Dùng bounded channel + `DropWrite` + metric overflow thay vì để OOM.
7. **Body webhook không giới hạn.** Chặn theo `MaxBodyBytes` trước khi buffer.

## Metrics

Meter `UWB.ANPR.Ingestion`:
`anpr.events.received`, `anpr.events.duplicate`, `anpr.events.persist_failed`,
`anpr.queue.overflow`, `anpr.stream.reconnect`, `anpr.camera.auth_failed`.

## Ghi chú kỹ thuật

- Target framework: **`net9.0`**.
- `TreatWarningsAsErrors` hiện tắt để scaffold build sạch; nên bật khi code đã ổn định.
- CI (`.github/workflows/ci.yml`) chạy `restore` → `build` → `test` trên mỗi push/PR.

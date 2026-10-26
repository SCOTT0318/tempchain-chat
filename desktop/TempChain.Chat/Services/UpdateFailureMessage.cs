using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace TempChain.Chat.Services;

public static class UpdateFailureMessage
{
    public static string For(Exception error) => error switch
    {
        OperationCanceledException => "업데이트 확인이 취소되었거나 응답 시간이 초과됐습니다.",
        InvalidDataException => "업데이트 배포 파일 또는 체크섬을 확인하지 못했습니다. 릴리스 페이지에서 TChat 설치 파일을 직접 내려받아 주세요.",
        JsonException or FormatException or ArgumentException => "업데이트 서버의 릴리스 정보 형식이 올바르지 않습니다. 잠시 후 다시 확인해 주세요.",
        InvalidOperationException => error.Message,
        UnauthorizedAccessException => "업데이트 파일을 저장할 권한이 없습니다. 사용자 폴더의 쓰기 권한을 확인해 주세요.",
        HttpRequestException => "업데이트 서버에 연결하지 못했습니다. 인터넷 연결을 확인한 뒤 다시 시도해 주세요.",
        IOException => "업데이트 파일을 저장하지 못했습니다. 디스크 공간과 파일 사용 여부를 확인해 주세요.",
        _ => "업데이트를 완료하지 못했습니다. 잠시 후 다시 확인하거나 릴리스 페이지에서 직접 설치해 주세요."
    };
}

using System;

namespace app_desktop.Services;

internal sealed class RuntimeBundleUnavailableException : InvalidOperationException
{
	public RuntimeBundleUnavailableException(string step, int statusCode = 404)
		: base($"HTTP {statusCode} khi tải runtime {step}. Máy chủ chưa cung cấp thành phần runtime cần thiết; cần cập nhật server NestG và runtime bundle.")
	{
	}
}

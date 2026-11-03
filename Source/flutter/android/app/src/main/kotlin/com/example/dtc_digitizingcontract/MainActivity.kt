package com.example.dtc_digitizingcontract

import android.app.Activity
import android.content.Intent
import android.database.Cursor
import android.net.Uri
import android.provider.OpenableColumns
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File
import java.io.FileOutputStream

class MainActivity : FlutterActivity() {
    private val CHANNEL = "com.contrack/file_picker"
    private val PICK_FILE_REQUEST_CODE = 9021
    private var pendingResult: MethodChannel.Result? = null

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL)
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "pickImage" -> {
                        if (pendingResult != null) {
                            result.error("ALREADY_ACTIVE", "Trình chọn ảnh đang mở", null)
                            return@setMethodCallHandler
                        }
                        pendingResult = result
                        val intent = Intent(Intent.ACTION_GET_CONTENT).apply {
                            type = "image/*"
                            addCategory(Intent.CATEGORY_OPENABLE)
                        }
                        startActivityForResult(
                            Intent.createChooser(intent, "Chọn hình ảnh minh họa"),
                            PICK_FILE_REQUEST_CODE
                        )
                    }
                    "pickDocument" -> {
                        if (pendingResult != null) {
                            result.error("ALREADY_ACTIVE", "Trình chọn tệp đang mở", null)
                            return@setMethodCallHandler
                        }
                        pendingResult = result
                        val intent = Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
                            addCategory(Intent.CATEGORY_OPENABLE)
                            type = "*/*"
                            putExtra(
                                Intent.EXTRA_MIME_TYPES,
                                arrayOf("image/*", "application/pdf")
                            )
                        }
                        startActivityForResult(
                            Intent.createChooser(intent, "Chọn tệp đính kèm"),
                            PICK_FILE_REQUEST_CODE
                        )
                    }
                    else -> result.notImplemented()
                }
            }
    }

    @Deprecated("Deprecated in Java")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode != PICK_FILE_REQUEST_CODE) return

        val callback = pendingResult
        pendingResult = null
        if (callback == null) return

        if (resultCode != Activity.RESULT_OK || data?.data == null) {
            callback.success(null)
            return
        }

        val uri = data.data!!
        try {
            val fileName = resolveFileName(uri) ?: "anh_minh_hoa_${System.currentTimeMillis()}.jpg"
            val cachedPath = copyUriToCache(uri, fileName)
            val response = mapOf(
                "name" to fileName,
                "path" to cachedPath
            )
            callback.success(response)
        } catch (e: Exception) {
            callback.error("PICK_ERROR", e.localizedMessage, null)
        }
    }

    private fun resolveFileName(uri: Uri): String? {
        var name: String? = null
        if (uri.scheme == "content") {
            val cursor: Cursor? = contentResolver.query(uri, null, null, null, null)
            cursor?.use {
                if (it.moveToFirst()) {
                    val idx = it.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                    if (idx >= 0) {
                        name = it.getString(idx)
                    }
                }
            }
        }
        if (name == null) {
            name = uri.path?.substringAfterLast('/')
        }
        return name
    }

    private fun copyUriToCache(uri: Uri, fileName: String): String? {
        return try {
            val safeName = fileName.replace(Regex("[^a-zA-Z0-9._-]"), "_")
            val outFile = File(cacheDir, "${System.currentTimeMillis()}_$safeName")
            contentResolver.openInputStream(uri)?.use { input ->
                FileOutputStream(outFile).use { output ->
                    input.copyTo(output)
                }
            }
            outFile.absolutePath
        } catch (_: Exception) {
            null
        }
    }
}

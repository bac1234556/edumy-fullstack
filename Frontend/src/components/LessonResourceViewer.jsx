import React, { useState, useEffect } from 'react';
import api from '../api/axiosConfig';
import { detectLessonResourceType, normalizeLessonResource, formatBytes } from '../utils/resourceUtils';
import { FileX, AlertTriangle, ExternalLink, Download, FileText, RotateCw, Loader2, ShieldAlert, Presentation } from 'lucide-react';
import './LessonResourceViewer.css';

export default function LessonResourceViewer({ lesson: rawLesson }) {
  const [objectUrl, setObjectUrl] = useState(null);
  const [loading, setLoading] = useState(false);
  const [hasError, setHasError] = useState(false);
  const [errorCode, setErrorCode] = useState(null);

  const lesson = normalizeLessonResource(rawLesson);
  const resourceType = lesson?.resourceType || 'none';

  useEffect(() => {
    setHasError(false);
    setErrorCode(null);

    // Revoke previous object URL if any
    if (objectUrl) {
      URL.revokeObjectURL(objectUrl);
      setObjectUrl(null);
    }

    if (!lesson || !lesson.lessonId || resourceType === 'none' || lesson.resourceExists === false) {
      if (lesson?.resourceExists === false) {
        setHasError(true);
        setErrorCode('LESSON_RESOURCE_MISSING');
      }
      return;
    }

    let isMounted = true;
    const controller = new AbortController();

    async function fetchResourceBlob() {
      try {
        setLoading(true);
        const rawEndpoint = lesson.resourceEndpoint || `/learning/lessons/${lesson.lessonId}/resource`;
        const endpoint = rawEndpoint.replace(/^\/?api\//, '/');
        const response = await api.get(endpoint, {
          responseType: 'blob',
          signal: controller.signal
        });

        if (!isMounted) return;

        const blob = response.data;
        if (blob && (blob instanceof Blob || typeof blob === 'object') && blob.size > 0) {
          const newUrl = URL.createObjectURL(blob);
          setObjectUrl(newUrl);
          setLoading(false);
        } else {
          setLoading(false);
          setHasError(true);
          setErrorCode('LESSON_RESOURCE_MISSING');
        }
      } catch (err) {
        if (!isMounted || err.name === 'CanceledError' || err.code === 'ERR_CANCELED') return;
        setLoading(false);
        setHasError(true);

        const status = err.response?.status;
        const code = err.response?.data?.code;

        if (status === 410 || code === 'LESSON_RESOURCE_MISSING') {
          setErrorCode('LESSON_RESOURCE_MISSING');
        } else if (status === 404 || code === 'LESSON_RESOURCE_NOT_ATTACHED') {
          setErrorCode('LESSON_RESOURCE_NOT_ATTACHED');
        } else if (status === 403 || code === 'FORBIDDEN') {
          setErrorCode('FORBIDDEN');
        } else {
          setErrorCode('ERROR');
        }
      }
    }

    fetchResourceBlob();

    return () => {
      isMounted = false;
      controller.abort();
    };
  }, [lesson?.lessonId, lesson?.fileUrl]);

  if (!lesson) {
    return (
      <div className="resource-placeholder resource-none">
        <FileX size={56} color="#94a3b8" />
        <h4>Chưa chọn bài học</h4>
      </div>
    );
  }

  if (resourceType === 'none' || errorCode === 'LESSON_RESOURCE_NOT_ATTACHED' || !lesson.hasResource) {
    return (
      <div className="resource-placeholder resource-none">
        <FileX size={56} color="#94a3b8" className="mb-2" />
        <h4>Bài học này chưa có tài nguyên đính kèm</h4>
        <p className="text-muted">
          Giảng viên chưa tải lên video, hình ảnh hoặc tài liệu cho bài học này.
          <br />
          Bạn vẫn có thể xem các thông tin khác của bài học.
        </p>
      </div>
    );
  }

  if (errorCode === 'FORBIDDEN') {
    return (
      <div className="resource-placeholder resource-error">
        <ShieldAlert size={56} color="#f59e0b" className="mb-2" />
        <h4 className="text-warning">Không có quyền truy cập bài học</h4>
        <p className="text-muted">Bạn chưa đăng ký khóa học này hoặc không có quyền truy cập bài học.</p>
      </div>
    );
  }

  if (hasError || errorCode === 'LESSON_RESOURCE_MISSING' || lesson?.resourceExists === false) {
    return (
      <div className="resource-placeholder resource-error">
        <AlertTriangle size={56} color="#ef4444" className="mb-2" />
        <h4 className="text-danger">Không thể tải tài nguyên của bài học</h4>
        <p className="text-muted">
          Tệp có thể đã bị xóa, di chuyển hoặc không còn tồn tại trên máy chủ.
          <br />
          Vui lòng liên hệ giảng viên hoặc yêu cầu tải lại tài nguyên.
        </p>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="resource-placeholder resource-loading">
        <Loader2 size={48} color="#3b82f6" className="spin-icon mb-2" />
        <h4>Đang tải tài nguyên bài học...</h4>
        <p className="text-muted small">Vui lòng chờ trong giây lát.</p>
      </div>
    );
  }

  if (!objectUrl) {
    return null;
  }

  if (resourceType === 'video') {
    return (
      <div className="resource-container video-container">
        <video
          key={lesson.lessonId}
          controls
          preload="metadata"
          src={objectUrl}
          className="lesson-video-player"
          onError={() => setHasError(true)}
        >
          Trình duyệt của bạn không hỗ trợ phát video HTML5.
        </video>
      </div>
    );
  }

  if (resourceType === 'image') {
    return (
      <div className="resource-container image-container">
        <div className="image-view-box">
          <img
            key={lesson.lessonId}
            src={objectUrl}
            alt={lesson.originalFileName || lesson.title || 'Lesson image'}
            data-testid="lesson-resource-image"
            className="lesson-image-view"
            onError={() => setHasError(true)}
          />
        </div>
        <div className="resource-toolbar">
          <a
            href={objectUrl}
            target="_blank"
            rel="noreferrer"
            className="btn btn-sm btn-outline-light"
          >
            <ExternalLink size={14} className="me-1" /> Mở ảnh trong tab mới
          </a>
          <a
            href={objectUrl}
            download={lesson.originalFileName || `${lesson.title}.png`}
            className="btn btn-sm btn-primary ms-2"
          >
            <Download size={14} className="me-1" /> Tải ảnh về
          </a>
        </div>
      </div>
    );
  }

  if (resourceType === 'pdf') {
    return (
      <div className="resource-container pdf-container">
        <div className="pdf-view-box">
          <iframe
            key={lesson.lessonId}
            src={objectUrl}
            title={lesson.originalFileName || lesson.title || 'PDF Document'}
            className="lesson-pdf-iframe"
            onError={() => setHasError(true)}
          />
        </div>
        <div className="resource-toolbar d-flex justify-content-end gap-2">
          <a
            href={objectUrl}
            target="_blank"
            rel="noreferrer"
            className="btn btn-sm btn-outline-light"
          >
            <ExternalLink size={14} className="me-1" /> Mở PDF trong tab mới
          </a>
          <a
            href={objectUrl}
            download={lesson.originalFileName || `${lesson.title}.pdf`}
            className="btn btn-sm btn-primary"
          >
            <Download size={14} className="me-1" /> Tải xuống PDF
          </a>
        </div>
      </div>
    );
  }

  const isPpt = resourceType?.toLowerCase() === 'powerpoint' ||
                (lesson.originalFileName && (lesson.originalFileName.toLowerCase().endsWith('.ppt') || lesson.originalFileName.toLowerCase().endsWith('.pptx'))) || 
                (lesson.contentType && (lesson.contentType === 'application/vnd.ms-powerpoint' || 
                                        lesson.contentType === 'application/vnd.openxmlformats-officedocument.presentationml.presentation'));

  if (isPpt) {
    return (
      <div className="resource-container file-container">
        <div className="file-display-card">
          <Presentation size={64} color="#d04423" className="file-icon" />
          <h4 className="file-title">{lesson.originalFileName || lesson.title}</h4>
          <div className="file-meta">
            <span className="badge bg-danger me-2">
              PowerPoint
            </span>
            {lesson.fileSizeBytes && (
              <span className="text-muted">({formatBytes(lesson.fileSizeBytes)})</span>
            )}
          </div>
          <div className="file-actions mt-3 d-flex gap-2 justify-content-center">
            <a
              href={objectUrl}
              target="_blank"
              rel="noreferrer"
              className="btn btn-outline-primary"
            >
              <ExternalLink size={16} className="me-1" /> Mở file
            </a>
            <a
              href={objectUrl}
              download={lesson.originalFileName || 'presentation.pptx'}
              className="btn btn-primary"
            >
              <Download size={16} className="me-1" /> Tải xuống
            </a>
          </div>
        </div>
      </div>
    );
  }

  // Document & File generic viewer
  return (
    <div className="resource-container file-container">
      <div className="file-display-card">
        <FileText size={64} color="#3b82f6" className="file-icon" />
        <h4 className="file-title">{lesson.originalFileName || lesson.title}</h4>
        <div className="file-meta">
          <span className="badge bg-secondary me-2">
            {resourceType === 'document' ? 'Tài liệu' : 'Tập tin'}
          </span>
          {lesson.contentType && <span className="text-muted me-2">{lesson.contentType}</span>}
          {lesson.fileSizeBytes && (
            <span className="text-muted">({formatBytes(lesson.fileSizeBytes)})</span>
          )}
        </div>
        <div className="file-actions mt-3 d-flex gap-2 justify-content-center">
          <a
            href={objectUrl}
            target="_blank"
            rel="noreferrer"
            className="btn btn-outline-primary"
          >
            <ExternalLink size={16} className="me-1" /> Xem tập tin
          </a>
          <a
            href={objectUrl}
            download={lesson.originalFileName || 'document'}
            className="btn btn-primary"
          >
            <Download size={16} className="me-1" /> Tải xuống
          </a>
        </div>
      </div>
    </div>
  );
}

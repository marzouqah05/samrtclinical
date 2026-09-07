import React from 'react';
import { useApp } from '../context/AppContext';
import { X, Download, ZoomIn, ZoomOut, RotateCw } from 'lucide-react';

export const FileViewerModal: React.FC = () => {
  const { previewFile, setPreviewFile, lang, addToast } = useApp();
  const isAr = lang === 'ar';

  if (!previewFile) return null;

  return (
    <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4 z-50 animate-in fade-in">
      <div className="bg-[#0F172A] text-white rounded-xl border border-[#334155] max-w-3xl w-full flex flex-col overflow-hidden shadow-2xl">
        {/* Top bar */}
        <div className="flex justify-between items-center px-4 py-3 border-b border-[#334155] bg-[#1E293B]">
          <div>
            <h4 className="text-[14px] font-bold text-white">{previewFile.name}</h4>
            <p className="text-[11px] text-[#94A3B8]">
              {previewFile.size} • {isAr ? 'صورة أشعة سريرية' : 'Clinical Radiology Image'}
            </p>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() =>
                addToast(
                  isAr ? 'تم تنزيل الصورة' : 'Image Downloaded',
                  previewFile.name,
                  'success'
                )
              }
              className="p-1.5 text-[#94A3B8] hover:text-white hover:bg-[#334155] rounded-md transition-colors cursor-pointer"
              title={isAr ? 'تنزيل' : 'Download'}
            >
              <Download className="w-4 h-4" />
            </button>
            <button
              onClick={() => setPreviewFile(null)}
              className="p-1.5 text-[#94A3B8] hover:text-white hover:bg-[#334155] rounded-md transition-colors cursor-pointer"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Image Preview Canvas */}
        <div className="p-6 flex items-center justify-center bg-black/60 min-h-[380px] max-h-[70vh] overflow-auto">
          <img
            src={previewFile.url}
            alt={previewFile.name}
            className="max-h-[60vh] max-w-full object-contain rounded border border-[#334155] shadow-lg"
          />
        </div>

        {/* Footer controls */}
        <div className="px-4 py-2.5 bg-[#1E293B] border-t border-[#334155] flex justify-between items-center text-[12px] text-[#94A3B8]">
          <span>{isAr ? 'عرض فائق الدقة DICOM / JPG' : 'High Resolution DICOM / JPG View'}</span>
          <button
            onClick={() => setPreviewFile(null)}
            className="px-3 py-1 bg-[#334155] hover:bg-[#475569] text-white rounded text-[12px] font-medium"
          >
            {isAr ? 'إغلاق' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
};

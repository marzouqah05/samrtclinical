import React from 'react';
import { useApp } from '../context/AppContext';
import { CheckCircle2, AlertCircle, Info, XCircle, X } from 'lucide-react';

export const Toast: React.FC = () => {
  const { toasts, removeToast, lang } = useApp();
  const isAr = lang === 'ar';

  if (toasts.length === 0) return null;

  return (
    <div
      className={`fixed bottom-4 z-50 flex flex-col gap-2 pointer-events-none max-w-sm w-full px-4 ${
        isAr ? 'left-0' : 'right-0'
      }`}
    >
      {toasts.map((toast) => {
        const isSuccess = toast.type === 'success';
        const isError = toast.type === 'error';
        const isWarning = toast.type === 'warning';

        return (
          <div
            key={toast.id}
            className={`pointer-events-auto bg-white border rounded-xl p-3.5 shadow-xl flex items-start gap-3 animate-in slide-in-from-bottom-2 fade-in ${
              isSuccess
                ? 'border-[#DCFCE7] shadow-green-900/5'
                : isError
                ? 'border-[#FEE2E2]'
                : isWarning
                ? 'border-[#FEF3C7]'
                : 'border-[#dae2fd]'
            }`}
          >
            <div className="shrink-0 mt-0.5">
              {isSuccess && <CheckCircle2 className="w-4 h-4 text-[#166534]" />}
              {isError && <XCircle className="w-4 h-4 text-[#BA1A1A]" />}
              {isWarning && <AlertCircle className="w-4 h-4 text-[#92400E]" />}
              {!isSuccess && !isError && !isWarning && <Info className="w-4 h-4 text-[#006194]" />}
            </div>

            <div className="flex-1 min-w-0">
              <h5 className="text-[13px] font-bold text-[#0F172A] leading-tight">{toast.title}</h5>
              {toast.message && (
                <p className="text-[12px] text-[#64748B] mt-0.5 leading-snug">{toast.message}</p>
              )}
            </div>

            <button
              onClick={() => removeToast(toast.id)}
              className="text-[#64748B] hover:text-[#0F172A] p-0.5 rounded cursor-pointer"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        );
      })}
    </div>
  );
};

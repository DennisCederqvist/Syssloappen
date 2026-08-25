import { HttpInterceptorFn } from '@angular/common/http';

// Authentication is cookie-based, so every API request must include credentials.
export const credentialsInterceptor: HttpInterceptorFn = (request, next) =>
  next(request.clone({ withCredentials: true }));

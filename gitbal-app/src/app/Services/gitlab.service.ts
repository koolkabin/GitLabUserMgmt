// gitlab.service.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class GitlabService {
  private apiUrl = 'https://api-gitlabusers.essencetechnologies.com';

  constructor(private http: HttpClient) {}

  getProfile(token: string,username: string): Observable<any> {
    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);
    return this.http.get(`${this.apiUrl}/user/${username}`, { headers });
  }

  getProjects(token: string): Observable<any> {
    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);
    return this.http.get(`${this.apiUrl}/projects`, { headers });
  }
  removeUser(token: string, username: string): Observable<any> {
    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);
    return this.http.delete(`${this.apiUrl}/removeuser/${username}`, { headers });
  }
}

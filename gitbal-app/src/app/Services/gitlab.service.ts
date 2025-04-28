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
  getEventStream(): Observable<any> {
    return new Observable((observer) => {
      const eventSource = new EventSource(`${this.apiUrl}/stream`); // Replace with your SSE endpoint

      eventSource.onmessage = (event) => {
        observer.next(event.data); // Send the data to the subscriber
      };

      eventSource.onerror = (error) => {
        observer.error(error); // Handle any errors
        eventSource.close(); // Close the connection if error occurs
      };
    });
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NotaFiscal, NotaPayload, Produto, ProdutoPayload } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly apiEstoque = 'http://localhost:5001/api/produtos';
  private readonly apiNotas = 'http://localhost:5002/api/notas';

  constructor(private readonly http: HttpClient) {}

  listarProdutos(): Observable<Produto[]> {
    return this.http.get<Produto[]>(this.apiEstoque);
  }

  criarProduto(payload: ProdutoPayload): Observable<Produto> {
    return this.http.post<Produto>(this.apiEstoque, payload);
  }

  listarNotas(): Observable<NotaFiscal[]> {
    return this.http.get<NotaFiscal[]>(this.apiNotas);
  }

  criarNota(payload: NotaPayload): Observable<NotaFiscal> {
    return this.http.post<NotaFiscal>(this.apiNotas, payload);
  }

  imprimirNota(id: number): Observable<unknown> {
    return this.http.post(`${this.apiNotas}/${id}/imprimir`, {});
  }
}

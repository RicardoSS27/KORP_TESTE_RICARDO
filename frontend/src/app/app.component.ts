import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ItemNota, NotaFiscal, Produto } from './models';

type MessageType = 'success' | 'error' | 'info';

interface UiMessage {
  type: MessageType;
  text: string;
}

interface NotaItemForm {
  itemId: number;
  produtoId: number | null;
  quantidade: number | null;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  produtos: Produto[] = [];
  notas: NotaFiscal[] = [];

  codigo = '';
  descricao = '';
  saldo: number | null = null;
  numeroNota: number | null = null;
  itensNota: NotaItemForm[] = [];

  produtoMessage: UiMessage | null = null;
  notaMessage: UiMessage | null = null;

  salvandoProduto = false;
  salvandoNota = false;
  imprimindoNotaId: number | null = null;

  private itemCounter = 0;

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    this.adicionarItemNota();
    this.carregarDashboard();
  }

  get notasOrdenadas(): NotaFiscal[] {
    return [...this.notas].sort((a, b) => b.id - a.id);
  }

  get totalNotasAbertas(): number {
    return this.notas.filter((nota) => nota.status === 'Aberta').length;
  }

  carregarProdutos(exibirMensagem = false): void {
    this.api.listarProdutos().subscribe({
      next: (produtos) => {
        this.produtos = produtos;
        if (exibirMensagem) {
          this.produtoMessage = { type: 'success', text: 'Lista de produtos atualizada.' };
        }
      },
      error: () => {
        this.produtoMessage = { type: 'error', text: 'Nao foi possivel carregar os produtos.' };
      }
    });
  }

  carregarNotas(exibirMensagem = false): void {
    this.api.listarNotas().subscribe({
      next: (notas) => {
        this.notas = notas;
        if (exibirMensagem) {
          this.notaMessage = { type: 'success', text: 'Lista de notas atualizada.' };
        }
      },
      error: () => {
        this.notaMessage = { type: 'error', text: 'Nao foi possivel carregar as notas fiscais.' };
      }
    });
  }

  criarProduto(): void {
    const codigo = this.codigo.trim();
    const descricao = this.descricao.trim();
    const saldo = this.saldo;

    if (!codigo || !descricao || !Number.isInteger(saldo) || (saldo ?? -1) < 0) {
      this.produtoMessage = { type: 'info', text: 'Preencha codigo, descricao e saldo inicial valido.' };
      return;
    }

    this.salvandoProduto = true;
    this.produtoMessage = { type: 'info', text: 'Salvando produto...' };

    this.api.criarProduto({ codigo, descricao, saldo: saldo ?? 0 })
      .pipe(finalize(() => { this.salvandoProduto = false; }))
      .subscribe({
        next: () => {
          this.limparFormularioProduto();
          this.carregarProdutos();
          this.produtoMessage = { type: 'success', text: 'Produto salvo com sucesso.' };
        },
        error: () => {
          this.produtoMessage = { type: 'error', text: 'Nao foi possivel salvar o produto.' };
        }
      });
  }

  criarNota(): void {
    const numero = this.numeroNota;
    const itens = this.coletarItensNota();

    if (!Number.isInteger(numero) || (numero ?? 0) <= 0) {
      this.notaMessage = { type: 'info', text: 'Informe um numero valido para a nota.' };
      return;
    }

    if (!itens.length) {
      this.notaMessage = { type: 'info', text: 'Adicione ao menos um item para criar a nota.' };
      return;
    }

    const itensInvalidos = itens.some((item) =>
      !Number.isInteger(item.produtoId) ||
      item.produtoId <= 0 ||
      !Number.isInteger(item.quantidade) ||
      item.quantidade <= 0
    );

    if (itensInvalidos) {
      this.notaMessage = { type: 'info', text: 'Selecione um produto e quantidade valida em todos os itens.' };
      return;
    }

    this.salvandoNota = true;
    this.notaMessage = { type: 'info', text: 'Salvando nota fiscal...' };

    this.api.criarNota({ numero: numero ?? 0, itens })
      .pipe(finalize(() => { this.salvandoNota = false; }))
      .subscribe({
        next: () => {
          this.limparFormularioNota();
          this.carregarDashboard();
          this.notaMessage = { type: 'success', text: 'Nota fiscal criada com sucesso.' };
        },
        error: () => {
          this.notaMessage = { type: 'error', text: 'Nao foi possivel salvar a nota fiscal.' };
        }
      });
  }

  imprimirNota(id: number): void {
    this.imprimindoNotaId = id;
    this.notaMessage = { type: 'info', text: `Imprimindo nota ${id}...` };

    this.api.imprimirNota(id)
      .pipe(finalize(() => { this.imprimindoNotaId = null; }))
      .subscribe({
        next: () => {
          this.carregarDashboard();
          this.notaMessage = { type: 'success', text: `Nota ${id} impressa e fechada com sucesso.` };
        },
        error: (error: HttpErrorResponse) => {
          const detalhe = this.extrairMensagemErro(error);
          this.notaMessage = { type: 'error', text: `Nao foi possivel imprimir a nota ${id}. ${detalhe}` };
        }
      });
  }

  adicionarItemNota(): void {
    this.itemCounter += 1;
    this.itensNota.push({
      itemId: this.itemCounter,
      produtoId: null,
      quantidade: null
    });
  }

  removerItemNota(itemId: number): void {
    this.itensNota = this.itensNota.filter((item) => item.itemId !== itemId);
  }

  produtoDescricao(produtoId: number): string {
    return this.produtos.find((produto) => produto.id === produtoId)?.descricao ?? `Produto ${produtoId}`;
  }

  private carregarDashboard(): void {
    forkJoin({
      produtos: this.api.listarProdutos(),
      notas: this.api.listarNotas()
    }).subscribe({
      next: ({ produtos, notas }) => {
        this.produtos = produtos;
        this.notas = notas;
      },
      error: () => {
        if (!this.produtos.length) {
          this.produtoMessage = { type: 'error', text: 'Nao foi possivel carregar os produtos.' };
        }

        if (!this.notas.length) {
          this.notaMessage = { type: 'error', text: 'Nao foi possivel carregar as notas fiscais.' };
        }
      }
    });
  }

  private limparFormularioProduto(): void {
    this.codigo = '';
    this.descricao = '';
    this.saldo = null;
  }

  private limparFormularioNota(): void {
    this.numeroNota = null;
    this.itensNota = [];
    this.itemCounter = 0;
    this.adicionarItemNota();
  }

  private coletarItensNota(): ItemNota[] {
    return this.itensNota.map((item) => ({
      produtoId: Number(item.produtoId),
      quantidade: Number(item.quantidade)
    }));
  }

  private extrairMensagemErro(error: HttpErrorResponse): string {
    if (typeof error.error === 'string' && error.error.trim()) {
      return error.error;
    }

    return 'Falha ao concluir a operacao.';
  }
}
